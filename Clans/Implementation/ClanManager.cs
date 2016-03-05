using System;
using System.Data;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using System.Linq;
using TShockAPI;
using System.IO;
using TShockAPI.DB;
using Clans.Hooks;
using Newtonsoft.Json;
using MySql.Data.MySqlClient;
using System.Timers;
using Terraria;

namespace Clans {
  public static class ClanManager {
    private static IDbConnection Database;
    public static Config Config = new Config();
    public static string SavePath = Path.Combine(TShock.SavePath, "Clans");
    public static Dictionary<string, Clan> Clans;
    public static Dictionary<int, ClanMember> ClanMembers;
    private static Timer timer;
    public static ClanInvite[] PendingInvites = new ClanInvite[Main.maxPlayers];

    public static void Initialize() {
      if (!Directory.Exists(SavePath))
        Directory.CreateDirectory(SavePath);

      switch (TShock.Config.StorageType.ToLower()) {
        case "mysql":
          string[] host = TShock.Config.MySqlHost.Split(':');
          Database = new MySqlConnection() {
            ConnectionString = string.Format("Server={0}; Port={1}; Database={2}; Uid={3}; Pwd={4};",
                      host[0],
                      host.Length == 1 ? "3306" : host[1],
                      TShock.Config.MySqlDbName,
                      TShock.Config.MySqlUsername,
                      TShock.Config.MySqlPassword)
          };
          break;
        case "sqlite":
          string sql = Path.Combine(SavePath, "Clans.sqlite");
          Database = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));
          break;
      }
      SqlTableCreator SQLcreator = new SqlTableCreator(Database, Database.GetSqlType() == SqlType.Sqlite
      ? (IQueryBuilder)new SqliteQueryCreator() : new MysqlQueryCreator());

      SqlTable[] tables = new SqlTable[]
      {
                 new SqlTable("Clans",
                     new SqlColumn("Name", MySqlDbType.VarChar, Config.ClanNameLength) { Primary=true, Unique=true },
                     new SqlColumn("Tag", MySqlDbType.Text),
                     new SqlColumn("Owner", MySqlDbType.Text),
                     new SqlColumn("InviteMode", MySqlDbType.Int32),
                     new SqlColumn("TileX", MySqlDbType.Int32),
                     new SqlColumn("TileY", MySqlDbType.Int32),
                     new SqlColumn("ChatColor", MySqlDbType.Text),
                     new SqlColumn("Bans", MySqlDbType.Text)
                     ),
                 new SqlTable("ClanMembers",
                     new SqlColumn("UserID",MySqlDbType.Int32) { Primary=true, Unique=true },
                     new SqlColumn("PlayerName", MySqlDbType.Text),
                     new SqlColumn("ClanName", MySqlDbType.Text)
                     ),
                 new SqlTable("ClanWarps",
                     new SqlColumn("WarpName",MySqlDbType.Text)
                     )
      };

      for (int i = 0; i < tables.Length; i++)
        SQLcreator.EnsureTableStructure(tables[i]);

      Config = Config.Read();
      Config.write();
      Clans = new Dictionary<string, Clan>();
      ClanMembers = new Dictionary<int, ClanMember>();
      LoadMembers();
      LoadClans();

      timer = new Timer(3000);
      timer.Elapsed += OnElapsed;
      timer.Start();

      for (int i = 0; i < PendingInvites.Length; i++) {
        PendingInvites[i] = new ClanInvite();
        PendingInvites[i].Timeout = 0;
      }
    }

    static void OnElapsed(object sender, ElapsedEventArgs e) {
      for (int i = 0; i < PendingInvites.Length; i++) {
        if (PendingInvites[i].Timeout <= 0)
          continue;

        TShock.Players[i].SendInfoMessage("Clan {0} has invited you.", PendingInvites[i].InvitingClan);
        TShock.Players[i].SendInfoMessage("Type /clan acceptinvite or /clan denyinvite");
        PendingInvites[i].Timeout--;

        if (PendingInvites[i].Timeout <= 0)
          TShock.Players[i].SendInfoMessage("Waited too long, invite dropped.");
      }
    }

    static bool ParseColor(string colorstring, out Color color) {
      color = new Color(135, 214, 9);
      byte r, g, b;
      string[] array = colorstring.Split(',');
      if (array.Length != 3)
        return false;
      if (!byte.TryParse(array[0], out r) || !byte.TryParse(array[1], out g) || !byte.TryParse(array[2], out b))
        return false;
      color = new Color(r, g, b);
      return true;
    }

    public static void ReloadAll() {
      LoadMembers();
      LoadClans();
    }

    public static void ReloadConfig(TSPlayer ts) {
      Config = Config.Read();
      Config.write();
    }

    static void LoadClans() {
      Clans.Clear();

      try {
        using (var reader = Database.QueryReader("SELECT * FROM Clans")) {
          while (reader.Read()) {
            string name = reader.Get<string>("Name");
            string tag = reader.Get<string>("Tag");
            string owner = reader.Get<string>("Owner");
            int inviteMode = reader.Get<int>("InviteMode");
            int tileX = reader.Get<int>("TileX");
            int tileY = reader.Get<int>("TileY");
            string bans = reader.Get<string>("Bans");
            Color color;
            ParseColor(reader.Get<string>("ChatColor"), out color);

            Clan clan = new Clan(name, owner) {
              InviteMode = (InviteMode)inviteMode,
              Tag = tag,
              TileX = tileX,
              TileY = tileY,
              Color = color,
              Bans = JsonConvert.DeserializeObject<List<string>>(bans)
            };
            Clans.Add(name, clan);
          }
        }
      }
      catch (Exception ex) {
        TShock.Log.Error(ex.ToString());
      }
    }

    static void LoadMembers() {
      ClanMembers.Clear();

      try {
        using (var reader = Database.QueryReader("SELECT * FROM ClanMembers")) {
          while (reader.Read()) {
            int userID = reader.Get<int>("UserID");
            string playername = reader.Get<string>("PlayerName");
            string clanName = reader.Get<string>("ClanName");

            if (!ClanMembers.ContainsKey(userID))
              ClanMembers.Add(userID, new ClanMember(userID, playername, clanName));
          }
        }
      }
      catch (Exception ex) {
        TShock.Log.Error(ex.ToString());
      }
    }

    public static bool SetColor(Clan clan, string color) {
      Color temp;
      if (!ParseColor(color, out temp))
        return false;
      clan.Color = temp;
      Database.Query("UPDATE Clans SET ChatColor = @0 WHERE Name = @1", color, clan.Name);
      return true;
    }

    public static void Rename(Clan clan, string newname) {
      Database.Query("UPDATE Clans SET Name = @0 WHERE Name = @1", newname, clan.Name);
      Database.Query("UPDATE ClanMembers SET ClanName = @0 WHERE ClanName = @1", newname, clan.Name);
      ClanMembers.Values.Where(c => c.ClanName == clan.Name)
        .ForEach(delegate (ClanMember member) { member.ClanName = newname; });
      Clans.Remove(clan.Name);
      clan.Name = newname;
      Clans.Add(newname,clan);
    }

    public static void SetSpawn(Clan clan, int tileX, int tileY) {
      clan.TileX = tileX;
      clan.TileY = tileY;
      Database.Query("UPDATE Clans SET TileX = @0, TileY = @1 WHERE Name = @2", clan.TileX, clan.TileY, clan.Name);
    }

    public static void SetInviteMode(Clan clan, InviteMode mode) {
      if (clan.InviteMode == mode)
        return;
      clan.InviteMode = mode;
      Database.Query("UPDATE Clans SET InviteMode = @0 WHERE Name = @1", (int)mode, clan.Name);
    }

    public static void UpdateTag(Clan clan, string tag) {
      Database.Query("UPDATE Clans SET Tag = @0 WHERE Name = @1", tag, clan.Name);
      Clans[clan.Name].Tag = tag;
      try {
        using (var reader = Database.QueryReader("SELECT * FROM ClanMembers WHERE ClanName = @0", clan.Name)) {
          while (reader.Read()) {
            int userID = reader.Get<int>("UserID");
            UserSpecificFunctions.UserSpecificFunctions.LatestInstance.setUserSuffix(userID, tag);
          }
        }
      }
      catch (Exception ex) {
        TShock.Log.Error(ex.ToString());
      }
    }

    static void UpdateBans(Clan clan) {
      Database.Query("UPDATE Clans SET Bans = @0 WHERE Name = @1", JsonConvert.SerializeObject(clan.Bans), clan.Name);
    }

    public static bool InsertBan(Clan clan, string name) {
      if (clan.Bans.Contains(name))
        return false;
      clan.Bans.Add(name);
      UpdateBans(clan);
      return true;
    }

    public static bool InsertBan(Clan clan, ClanMember member) {
      if (clan.Bans.Contains(member.Name))
        return false;
      clan.Bans.Add(member.Name);
      LeaveClan(clan, member);
      UpdateBans(clan);
      return true;
    }

    public static bool RemoveBan(Clan clan, string playername) {
      if (!clan.Bans.Contains(playername))
        return false;
      clan.Bans.Remove(playername);
      UpdateBans(clan);
      return true;
    }

    public static bool CreateClan(Clan clan, ClanMember member) {
      try {
        Database.Query("INSERT INTO Clans (Name, Owner, InviteMode, ChatColor, Bans) VALUES (@0, @1, @2, @3, @4)", clan.Name, clan.Owner, (int)InviteMode.False, Config.DefaultChatColor, "[]");
        Clans.Add(clan.Name, clan);
        JoinClan(clan, member);
        ClanHooks.OnClanCreated(getClanMember(member.Name), clan.Name);
        return true;
      }
      catch (Exception ex) {
        TShock.Log.Error(ex.ToString());
        return false;
      }
    }

    public static bool JoinClan(Clan clan, ClanMember member) {
      try {
        if (ClanMembers.ContainsKey(member.UserID)) {
          ClanMembers[member.UserID] = member;
        } else {
          ClanMembers.Add(member.UserID, member);
        }
        Database.Query("INSERT INTO ClanMembers (UserID, PlayerName, ClanName) VALUES (@0, @1, @2)", member.UserID, member.Name, clan.Name);
        if (member.Name != clan.Owner)
          ClanHooks.OnClanJoin(member, clan);
        UpdateTag(clan, clan.Tag);
        return true;
      }
      catch (Exception ex) {
        TShock.Log.Error(ex.ToString());
        return false;
      }
    }

    public static void LeaveClan(Clan clan, ClanMember member) {
      try {
        ClanHooks.OnClanLeave(member, clan);
        if (member.Name == clan.Owner) {
          ClanHooks.OnClanRemoved(clan);
          RemoveClan(clan);
        }
        else {
          Database.Query("DELETE FROM ClanMembers WHERE UserID = @0 AND ClanName = @1", member.UserID, clan.Name);
        }

        if (ClanMembers.ContainsKey(member.UserID))
          ClanMembers.Remove(member.UserID);
        UserSpecificFunctions.UserSpecificFunctions.LatestInstance.removeUserSuffix(member.UserID);
      }
      catch (Exception ex) {
        TShock.Log.Error(ex.ToString());
      }
    }

    public static void RemoveClan(Clan clan) {
      UserSpecificFunctions.UserSpecificFunctions.LatestInstance.removeAllSuffix(clan.Tag);
      Database.Query("DELETE FROM ClanMembers WHERE ClanName = @0", clan.Name);
      Database.Query("DELETE FROM Clans WHERE Name = @0", clan.Name);
      Clans.Remove(clan.Name);
    }

    public static Clan FindClanByPlayer(int UserID) {
      if (!ClanMembers.ContainsKey(UserID))
        return null;

      return FindClanByName(ClanMembers[UserID].ClanName);
    }

    public static Clan FindClanByPlayer(TSPlayer ts) {
      if (ts == null)
        return null;

      if (!ClanMembers.ContainsKey(ts.User.ID))
        return null;

      return FindClanByName(ClanMembers[ts.User.ID].ClanName);
    }

    public static Clan FindClanByName(string name) {
      if (Clans.ContainsKey(name))
        return Clans[name];
      return null;
    }

    public static ClanMember getClanMember(string name) {
      return ClanMembers.Values.Where(x => x.Name == name).FirstOrDefault();
    }
  }
  
  public enum InviteMode {
    False = 0,
    True = 1
  }

  public class ClanInvite {
    public string InvitingClan;
    public int Timeout;

    public ClanInvite(string invitingClan = "") {
      InvitingClan = invitingClan;
      Timeout = 10;
    }
  }
}
