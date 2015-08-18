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
    public static Dictionary<string, Clan> Clans = new Dictionary<string, Clan>();
    public static Dictionary<int, string> ClanMembers = new Dictionary<int, string>();
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
      Clans.Clear();
      ClanMembers.Clear();
      LoadClans();
      for (int i = 0; i < TShock.Players.Length; i++) {
        if (TShock.Players[i] == null)
          continue;

        LoadMember(TShock.Players[i]);
      }
    }

    public static void ReloadConfig(TSPlayer ts) {
      Config = Config.Read();
      Config.write();
    }

    static void LoadClans() {
      try {
        using (var reader = Database.QueryReader("SELECT * FROM Clans")) {
          while (reader.Read()) {
            string name = reader.Get<string>("Name");
            string owner = reader.Get<string>("Owner");
            int inviteMode = reader.Get<int>("InviteMode");
            int tileX = reader.Get<int>("TileX");
            int tileY = reader.Get<int>("TileY");
            string bans = reader.Get<string>("Bans");
            Color color;
            ParseColor(reader.Get<string>("ChatColor"), out color);

            Clan clan = new Clan() {
              Name = name,
              Owner = owner,
              InviteMode = (InviteMode)inviteMode,
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

    public static bool SetColor(Clan clan, string color) {
      Color temp;
      if (!ParseColor(color, out temp))
        return false;
      clan.Color = temp;
      Database.Query("UPDATE Clans SET ChatColor = @0 WHERE Name = @1", color, clan.Name);
      return true;
    }

    public static void Rename(Clan clan, TSPlayer ts, string newname) {
      Database.Query("UPDATE Clans SET Name = @0 WHERE Name = @1", newname, clan.Name);
      Database.Query("UPDATE ClanMembers SET ClanName = @0 WHERE ClanName = @1", newname, clan.Name);
      UnLoadClan(clan);
      clan.Name = newname;
      LoadClan(clan);
    }

    public static void UnLoadClan(Clan clan) {
      Clans.Remove(clan.Name);
      int[] ids = ClanMembers.Where(c => c.Value == clan.Name).Select(c => c.Key).ToArray();
      for (int i = 0; i < ids.Length; i++)
        UnLoadMember(TShock.Players[ids[i]]);
    }

    public static void LoadClan(Clan clan) {
      Clans.Add(clan.Name, clan);
      foreach (TSPlayer ts in TShock.Players) {
        if (ts != null) {
          if (!ClanMembers.ContainsKey(ts.Index))
            LoadMember(ts);
        }
      }
    }

    public static void SetSpawn(Clan clan, TSPlayer ts) {
      clan.TileX = ts.TileX;
      clan.TileY = ts.TileY;
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
      UnLoadClan(clan);
      clan.Tag = tag;
      LoadClan(clan);

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

    public static bool InsertBan(Clan clan, TSPlayer ts) {
      if (clan.Bans.Contains(ts.User.Name))
        return false;
      clan.Bans.Add(ts.User.Name);
      LeaveClan(ts, clan);
      UpdateBans(clan);
      return true;
    }

    public static bool RemoveBan(Clan clan, TSPlayer ts) {
      if (!clan.Bans.Contains(ts.Name))
        return false;
      clan.Bans.Remove(ts.Name);
      UpdateBans(clan);
      return true;
    }

    public static bool CreateClan(TSPlayer ts, Clan clan) {
      try {
        Database.Query("INSERT INTO Clans (Name, Owner, InviteMode, ChatColor, Bans) VALUES (@0, @1, @2, @3, @4)", clan.Name, clan.Owner, (int)InviteMode.False, Config.DefaultChatColor, "[]");
        Clans.Add(clan.Name, clan);
        JoinClan(ts, clan);
        ClanHooks.OnClanCreated(ts, clan.Name);
        return true;
      }
      catch (Exception ex) {
        TShock.Log.Error(ex.ToString());
        return false;
      }
    }

    public static bool JoinClan(TSPlayer ts, Clan clan) {
      try {
        ClanMembers[ts.Index] = clan.Name;
        Database.Query("INSERT INTO ClanMembers (UserID, ClanName) VALUES (@0, @1)", ts.User.ID, clan.Name);
        clan.OnlineClanMembers.Add(ts.Index, new ClanMember() { Index = ts.Index, ClanName = ClanMembers[ts.Index] });
        if (ts.User.Name != clan.Owner)
          ClanHooks.OnClanJoin(ts, clan);
        UpdateTag(clan, clan.Tag);
        return true;
      }
      catch (Exception ex) {
        TShock.Log.Error(ex.ToString());
        return false;
      }
    }

    public static void LeaveClan(TSPlayer ts, Clan clan) {
      try {
        ClanHooks.OnClanLeave(ts, clan);
        if (ts.User.Name == clan.Owner) {
          ClanHooks.OnClanRemoved(clan);
          RemoveClan(clan);
        }
        else {
          UnLoadMember(ts);
          Database.Query("DELETE FROM ClanMembers WHERE UserID = @0 AND ClanName = @1", ts.User.ID, clan.Name);
        }
        ClanMembers[ts.Index] = string.Empty;
        UserSpecificFunctions.UserSpecificFunctions.LatestInstance.removeUserSuffix(ts.User.ID);
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

    public static void LoadMember(TSPlayer ts) {
      try {
        using (var reader = Database.QueryReader("SELECT * FROM ClanMembers WHERE UserID = @0", ts.User.ID)) {
          if (reader.Read()) {
            string clanName = reader.Get<string>("ClanName");
            if (!ClanMembers.ContainsKey(ts.Index))
              ClanMembers.Add(ts.Index, clanName);
            Clan c = FindClanByPlayer(ts);
            if (c != null) {
              c.OnlineClanMembers.Add(ts.Index, new ClanMember() { Index = ts.Index, ClanName = clanName });
              ClanHooks.OnClanLogin(ts, c);
            }
          }
          else {
            if (!ClanMembers.ContainsKey(ts.Index))
              ClanMembers.Add(ts.Index, string.Empty);
          }
        }
      }
      catch (Exception ex) {
        TShock.Log.Error(ex.ToString());
      }
    }

    public static void UnLoadMember(TSPlayer ts) {
      Clan c = FindClanByPlayer(ts);
      if (c != null)
        c.OnlineClanMembers.Remove(ts.Index);

      if (ClanMembers.ContainsKey(ts.Index))
        ClanMembers.Remove(ts.Index);
    }

    public static Clan FindClanByPlayer(TSPlayer ts) {
      if (ts == null)
        return null;

      if (!ClanMembers.ContainsKey(ts.Index))
        return null;

      return FindClanByName(ClanMembers[ts.Index]);
    }

    public static Clan FindClanByName(string name) {
      if (Clans.ContainsKey(name))
        return Clans[name];
      return null;
    }
  }

  public class Clan {
    public string Name { get; set; }
    public string Tag { get; set; }
    public string Owner { get; set; }
    public InviteMode InviteMode { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public Color Color { get; set; }
    public Dictionary<int, ClanMember> OnlineClanMembers { get; set; }
    public List<string> Bans { get; set; }

    public Clan() {
      OnlineClanMembers = new Dictionary<int, ClanMember>();
      Bans = new List<string>();
      Color = ClanManager.Config.ParseColor();
      Tag = "";
    }

    public bool IsInClan(int PlayerIndex) {
      return OnlineClanMembers.ContainsKey(PlayerIndex);
    }

    public bool IsBanned(string name) {
      return Bans.Contains(name);
    }

    public void Broadcast(string msg, int ExcludePlayer = -1) {
      foreach (KeyValuePair<int, ClanMember> kvp in OnlineClanMembers) {
        if (ExcludePlayer > -1 && kvp.Key == ExcludePlayer)
          continue;

        kvp.Value.TSPlayer.SendMessage(msg, Color);
      }
    }
  }

  public class ClanMember {
    public int Index { get; set; }
    public string ClanName { get; set; }
    public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
    public bool DefaultToClanChat { get; set; }
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
