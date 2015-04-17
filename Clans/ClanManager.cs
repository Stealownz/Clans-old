using System;
using System.Data;
using System.Collections.Generic;
using Mono.Data.Sqlite;
using System.Linq;
using System.Text;
using TShockAPI;
using System.IO;
using TShockAPI.DB;
using Clans.Hooks;
using Newtonsoft.Json;
using System.Net;
using MySql.Data.MySqlClient;
using System.Diagnostics;


namespace Clans
{
    public static class ClanManager
    {
        static IDbConnection Database;
        public static Config Config = new Config();
        public static string SavePath = Path.Combine(TShock.SavePath, "Clans");
        public static Dictionary<string, Clan> Clans = new Dictionary<string, Clan>();
        public static Dictionary<int, string> ClanMembers = new Dictionary<int, string>();

        public static void Initialize()
        {
            if (!Directory.Exists(SavePath))
                Directory.CreateDirectory(SavePath);

            switch (TShock.Config.StorageType.ToLower())
            {
                case "mysql":
                    string[] host = TShock.Config.MySqlHost.Split(':');
                    Database = new MySqlConnection()
                    {
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
                     new SqlColumn("Name", MySqlDbType.VarChar) { Primary=true, Unique=true },
                     new SqlColumn("Owner", MySqlDbType.VarChar),
                     new SqlColumn("InviteMode", MySqlDbType.Int32),
                     new SqlColumn("TileX", MySqlDbType.Int32),
                     new SqlColumn("TileY", MySqlDbType.Int32),
                     new SqlColumn("ChatColor", MySqlDbType.VarChar),
                     new SqlColumn("Bans", MySqlDbType.VarChar)
                     ),
                 new SqlTable("ClanMembers",
                     new SqlColumn("Username",MySqlDbType.VarChar) { Primary=true, Unique=true },
                     new SqlColumn("ClanName", MySqlDbType.VarChar)
                     ),
                 new SqlTable("ClanWarps",
                     new SqlColumn("WarpName",MySqlDbType.VarChar) { Primary=true, Unique=true }
                     )
            };

            for (int i = 0; i < tables.Length; i++)
                SQLcreator.EnsureTableStructure(tables[i]);

            Config = Config.Read();
            LoadClans();
        }

        static bool ParseColor(string colorstring, out Color color)
        {
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

        public static void ReloadAll()
        {
            Clans.Clear();
            ClanMembers.Clear();
            LoadClans();
            for (int i = 0; i < TShock.Players.Length; i++)
            {
                if (TShock.Players[i] == null)
                    continue;

                LoadMember(TShock.Players[i]);
            }
        }

        public static void ReloadConfig(TSPlayer ts)
        {
            Config = Config.Read(ts);
        }

        static void LoadClans()
        {
            try
            {
                using (var reader = Database.QueryReader("SELECT * FROM Clans"))
                {
                    while (reader.Read())
                    {
                        string name = reader.Get<string>("Name");
                        string owner = reader.Get<string>("Owner");
                        int inviteMode = reader.Get<int>("InviteMode");
                        int tileX = reader.Get<int>("TileX");
                        int tileY = reader.Get<int>("TileY");
                        string bans = reader.Get<string>("Bans");
                        Color color;
                        ParseColor(reader.Get<string>("ChatColor"), out color);

                        Clan clan = new Clan()
                        {
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
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static bool SetColor(Clan clan, string color)
        {
            Color temp;
            if (!ParseColor(color, out temp))
                return false;
            clan.Color = temp;
            Database.Query("UPDATE Clans SET ChatColor = @0 WHERE Name = @1", color, clan.Name);
            return true;
        }

        public static void Rename(Clan clan, TSPlayer ts, string newname)
        {
            Database.Query("UPDATE Clans SET Name = @0 WHERE Name = @1", newname ,clan.Name);
            Database.Query("UPDATE ClanMembers SET ClanName = @0 WHERE ClanName = @1",newname, clan.Name);
            UnLoadClan(clan);
            clan.Name = newname;
            LoadClan(clan);
        }

        public static void UnLoadClan(Clan clan)
        {
            Clans.Remove(clan.Name);
            int[] ids = ClanMembers.Where(c => c.Value == clan.Name).Select(c => c.Key).ToArray();
            for (int i = 0; i < ids.Length; i++)
                UnLoadMember(TShock.Players[ids[i]]);
        }

        public static void LoadClan(Clan clan)
        {
            Clans.Add(clan.Name, clan);
            foreach (TSPlayer ts in TShock.Players)
            {
                if (ts != null)
                {
                    if (!ClanMembers.ContainsKey(ts.Index))
                        LoadMember(ts);
                }
            }
        }

        public static void SetSpawn(Clan clan, TSPlayer ts)
        {           
            clan.TileX = ts.TileX;
            clan.TileY = ts.TileY;
            Database.Query("UPDATE Clans SET TileX = @0, TileY = @1 WHERE Name = @2", clan.TileX, clan.TileY, clan.Name);
        }

        public static void SetInviteMode(Clan clan, InviteMode mode)
        {
            if (clan.InviteMode == mode)
                return;
            clan.InviteMode = mode;
            Database.Query("UPDATE Clans SET InviteMode = @0 WHERE Name = @1", (int)mode, clan.Name);
        }

        static void UpdateBans(Clan clan)
        {
            Database.Query("UPDATE Clans SET Bans = @0 WHERE Name = @1", JsonConvert.SerializeObject(clan.Bans), clan.Name);
        }

        public static bool InsertBan(Clan clan, TSPlayer ts)
        {
            if (clan.Bans.Contains(ts.UserAccountName))
                return false;
            clan.Bans.Add(ts.UserAccountName);
            UpdateBans(clan);
            return true;
        }

        public static bool RemoveBan(Clan clan, string name)
        {
            if (clan.Bans.Contains(name))
                return false;
            clan.Bans.Remove(name);
            UpdateBans(clan);
            return true;
        }

        public static bool CreateClan(TSPlayer ts, Clan clan)
        {
            try
            {
                Database.Query("INSERT INTO Clans (Name, Owner, InviteMode, ChatColor, Bans) VALUES (@0, @1, @2, @3, @4)", clan.Name, clan.Owner, (int)InviteMode.False, Config.DefaultChatColor, "[]");
                Clans.Add(clan.Name, clan);
                JoinClan(ts, clan);
                ClanHooks.OnClanCreated(ts, clan.Name);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        public static bool JoinClan(TSPlayer ts, Clan clan)
        {
            try
            {
                ClanMembers[ts.Index] = clan.Name;
                Database.Query("INSERT INTO ClanMembers (Username, ClanName) VALUES (@0, @1)", ts.UserAccountName, clan.Name);
                clan.OnlineClanMembers.Add(ts.Index, new ClanMember() { Index = ts.Index, ClanName = ClanMembers[ts.Index] });
                if (ts.UserAccountName != clan.Owner)
                    ClanHooks.OnClanJoin(ts, clan);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return false;
            }
        }

        public static void LeaveClan(TSPlayer ts, Clan clan)
        {
            try
            {
                ClanHooks.OnClanLeave(ts, clan);
                if (ts.UserAccountName == clan.Owner)
                {
                    ClanHooks.OnClanRemoved(clan);
                    RemoveClan(clan);
                }
                else
                {
                    clan.OnlineClanMembers.Remove(ts.Index);
                    Database.Query("DELETE FROM ClanMembers WHERE Username = @0 AND ClanName = @1", ts.UserAccountName, clan.Name);
                }
                ClanMembers[ts.Index] = string.Empty;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static void RemoveClan(Clan clan)
        {
            Database.Query("DELETE FROM ClanMembers WHERE ClanName = @0", clan.Name);
            Database.Query("DELETE FROM Clans WHERE Name = @0", clan.Name);
            Clans.Remove(clan.Name);
        }

        public static void Kick(Clan clan, string name)
        {
            Database.Query("REMOVE FROM ClanMembers WHERE ClanName = @0 AND Username = @1", clan.Name, name);
        }

        public static void LoadMember(TSPlayer ts)
        {
            try
            {
                using (var reader = Database.QueryReader("SELECT * FROM ClanMembers WHERE Username = @0", ts.UserAccountName))
                {
                    if (reader.Read())
                    {
                        string clanName = reader.Get<string>("ClanName");
                        ClanMembers.Add(ts.Index, clanName);
                        Clan c = FindClanByPlayer(ts);
                        if (c != null)
                        {
                            c.OnlineClanMembers.Add(ts.Index, new ClanMember() { Index = ts.Index, ClanName = clanName });
                            ClanHooks.OnClanLogin(ts, c);
                        }
                    }
                    else
                        ClanMembers.Add(ts.Index, string.Empty);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static void UnLoadMember(TSPlayer ts)
        {
            Clan c = FindClanByPlayer(ts);
            if (c != null)
                c.OnlineClanMembers.Remove(ts.Index);
            ClanMembers.Remove(ts.Index);
        }

        public static Clan FindClanByPlayer(TSPlayer ts)
        {
            return FindClanByName(ClanMembers[ts.Index]);
        }

        public static Clan FindClanByName(string name)
        {
            if (Clans.ContainsKey(name))
                return Clans[name];
            return null;
        }
    }

    public class Clan
    {
        public string Name { get; set; }
        public string Owner { get; set; }
        public InviteMode InviteMode { get; set; }
        public int TileX { get; set; }
        public int TileY { get; set; }
        public Color Color { get; set; }
        public Dictionary<int, ClanMember> OnlineClanMembers { get; set; }
        public List<string> Bans { get; set; }

        public Clan()
        {
            OnlineClanMembers = new Dictionary<int, ClanMember>();
            Bans = new List<string>();
            Color = ClanManager.Config.ParseColor();
        }

        public bool IsInClan(int PlayerIndex)
        {
            return OnlineClanMembers.ContainsKey(PlayerIndex);
        }

        public bool IsBanned(string name)
        {
            return Bans.Contains(name);
        }

        public void Broadcast(string msg, int ExcludePlayer = -1)
        {
            foreach (KeyValuePair<int, ClanMember> kvp in OnlineClanMembers)
            {
                if (ExcludePlayer > -1 && kvp.Key == ExcludePlayer)
                    continue;

                kvp.Value.TSPlayer.SendMessage(msg, Color);
            }
        }
    }

    public class ClanMember
    {
        public int Index { get; set; }
        public string ClanName { get; set; }
        public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
        public bool DefaultToClanChat { get; set; }
    }

    public enum InviteMode
    {
        False = 0,
        True = 1
    }
}
