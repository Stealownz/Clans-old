using System.Collections.Generic;
using TerrariaApi.Server;
using System.Reflection;
using TShockAPI.Hooks;
using System.Linq;
using System.Text;
using Clans.Hooks;
using TShockAPI;
using TShockAPI.DB;
using Terraria;
using System;

namespace Clans {
  [ApiVersion(1, 22)]
  public class Clans : TerrariaPlugin {
    public override Version Version {
      get { return Assembly.GetExecutingAssembly().GetName().Version; }
    }
    public override string Author {
      get { return "Stealownz"; }
    }
    public override string Name {
      get { return "Clans"; }
    }
    public override string Description {
      get { return "Gives people the ability to create clans!"; }
    }

    public override void Initialize() {
      ClanHooks.ClanCreated += new ClanHooks.ClanCreatedD(ClanHooks_ClanCreated);
      ClanHooks.ClanLogin += new ClanHooks.ClanLoginD(ClanHooks_ClanLogin);
      ClanHooks.ClanJoin += new ClanHooks.ClanJoinD(ClanHooks_ClanJoin);
      ClanHooks.ClanLeave += new ClanHooks.ClanLeaveD(ClanHooks_ClanLeave);
      ClanHooks.ClanRemoved += new ClanHooks.ClanRemovedD(ClanHooks_ClanRemoved);

      ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
      ServerApi.Hooks.ServerChat.Register(this, OnChat);
      ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
      TShockAPI.Hooks.PlayerHooks.PlayerPostLogin += PlayerHooks_PlayerPostLogin;
    }

    protected override void Dispose(bool disposing) {
      if (disposing) {
        ServerApi.Hooks.ServerChat.Deregister(this, OnChat);
        ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
        ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
        TShockAPI.Hooks.PlayerHooks.PlayerPostLogin -= PlayerHooks_PlayerPostLogin;
      }
      base.Dispose(disposing);
    }

    public Clans(Main game)
        : base(game) {
      Order = 1;
    }

    #region Hooks
    void OnPostInitialize(EventArgs args) {
      Commands.ChatCommands.Add(new Command(Permission.Use, ClanCmd, "clan"));
      Commands.ChatCommands.Add(new Command(Permission.Chat, Chat, "c") { AllowServer = false });

      ClanManager.Initialize();
    }

    void PlayerHooks_PlayerPostLogin(PlayerPostLoginEventArgs args) {
      if (!ClanManager.ClanMembers.ContainsKey(args.Player.User.ID)) {
        return;
      }

      ClanManager.ClanMembers[args.Player.User.ID].Index = args.Player.Index;
      ClanHooks.OnClanLogin(ClanManager.getClanMember(args.Player.Name), ClanManager.FindClanByPlayer(args.Player));
    }

    void OnLeave(LeaveEventArgs e) {
      string name = Main.player[e.Who].name;

      ClanMember member = ClanManager.getClanMember(name);
      if (member == null)
        return;
      else
        member.Index = -1;

      ClanManager.PendingInvites[e.Who].Timeout = 0;
    }

    void OnChat(ServerChatEventArgs args) {
      TSPlayer ts = TShock.Players[args.Who];
      if (ts.mute || !ts.IsLoggedIn)
        return;

      Clan Myclan = ClanManager.FindClanByPlayer(ts);
      if (Myclan == null)
        return;

      if (args.Text.StartsWith(TShock.Config.CommandSpecifier))
        return;

      if (!ts.Group.HasPermission(Permission.Chat) || !ts.Group.HasPermission(Permission.Use)) {
        Myclan.OnlineClanMembers.Where(x => x.Index == ts.Index).First().DefaultToClanChat = false;
        return;
      }

      if (Myclan.OnlineClanMembers.Where(x => x.Index == ts.Index).First().DefaultToClanChat) {
        args.Handled = true;
        string msg = string.Format("[Clan] {0} - {1}: {2}", Myclan.Tag, ts.Name, string.Join(" ", args.Text));
        Myclan.Broadcast(msg);
        TShock.Utils.SendLogs(msg, Color.PaleVioletRed);
      }
    }
    #endregion
    
    //  Future Plans
    // Wormhole potion 
    // Add external admin commands.
    // force to team

    #region Commands
    void Chat(CommandArgs args) {
      Clan Myclan = ClanManager.FindClanByPlayer(args.Player);
      if (Myclan == null) {
        args.Player.SendErrorMessage("You are not in a clan!");
        return;
      }
      string msg = string.Format("[Clan] {0} - {1}: {2}", Myclan.Tag, args.Player.Name, string.Join(" ", args.Parameters));
      Myclan.Broadcast(msg);
      TShock.Utils.SendLogs(msg, Color.PaleVioletRed, args.Player);
    }

    void ClanCmd(CommandArgs args) {
      string cmd = args.Parameters.Count > 0 ? args.Parameters[0].ToLower() : "help";

      Clan MyClan = null;

      if (args.Player != TSPlayer.Server)
        MyClan = ClanManager.FindClanByPlayer(args.Player);

      switch (cmd) {
        #region create
        case "create":
          {
            if (!args.Player.Group.HasPermission(Permission.Create)) {
              args.Player.SendErrorMessage("You do not have permission to create a clan!");
              return;
            }
            if (args.Parameters.Count < 2) {
              args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan create <name>");
              return;
            }
            if (ClanManager.Config.MaxNumberOfClans > 0 && ClanManager.Clans.Keys.Count >= ClanManager.Config.MaxNumberOfClans) {
              args.Player.SendErrorMessage("The maximum amount of clans has been reached, sorry mate.");
              return;
            }
            string name = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
            if (MyClan != null) {
              args.Player.SendErrorMessage("You are already in a clan!");
              return;
            }
            if (name.Length >= ClanManager.Config.ClanNameLength) {
              args.Player.SendErrorMessage("Clan name is too long.");
              return;
            }
            if (ClanManager.FindClanByName(name) != null) {
              args.Player.SendErrorMessage("This clan already exists!");
              return;
            }
            if (!ClanManager.CreateClan(new Clan(name, args.Player.Name), new ClanMember(args.Player.User.ID, args.Player.Name, name, args.Player.Index)))
              args.Player.SendErrorMessage("Something went wrong! Please contact an administrator.");
          }
          break;
        #endregion create

        #region tag
        case "tag":
          {
            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }

            if (MyClan.Owner != args.Player.Name) {
              args.Player.SendErrorMessage("You are not the leader of the clan!");
              return;
            }

            if (args.Parameters.Count != 2 || args.Parameters[1] == "help") {
              args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan tag <tag>");
              return;
            }

            if (args.Parameters[1].Length > ClanManager.Config.ClanTagLength) {
              args.Player.SendErrorMessage("Clan Tag is too long. Max length is " + ClanManager.Config.ClanTagLength);
              return;
            }
            ClanManager.UpdateTag(MyClan, args.Parameters[1]);
            MyClan.Broadcast("Clan Tag updated.");
          }
          break;
        #endregion

        #region join
        case "join":
          {
            if (!args.Player.IsLoggedIn) {
              args.Player.SendErrorMessage("You need to be logged in to use this command!");
              return;
            }

            if (args.Parameters.Count < 2) {
              args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan join <clan name>");
              return;
            }
            string name = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
            if (MyClan != null) {
              args.Player.SendErrorMessage("You are already in a clan!");
              return;
            }
            Clan c = ClanManager.FindClanByName(name);
            if (c == null) {
              args.Player.SendErrorMessage("This clan does not exists!");
              return;
            }
            if (c.IsBanned(args.Player.User.Name)) {
              args.Player.SendErrorMessage("You have been banned from this clan!");
              return;
            }
            if (c.InviteMode == InviteMode.True) {
              args.Player.SendErrorMessage("This clan is in invite-only mode, please ask for an invitation.");
              return;
            }
            ClanManager.JoinClan(c, new ClanMember(args.Player.User.ID, args.Player.Name, c.Name, args.Player.Index));
          }
          break;
        #endregion join

        #region leave
        case "leave":
          {
            if (!args.Player.IsLoggedIn) {
              args.Player.SendErrorMessage("You need to be logged in to use this command!");
              return;
            }

            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }
            if (args.Parameters.Count == 2) {
              if (args.Parameters[1].ToLower() == "confirm") {
                ClanManager.LeaveClan(MyClan, MyClan.OnlineClanMembers.Where(x => x.Index == args.Player.Index).FirstOrDefault());
              }
              else
                args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan leave confirm");
            }
            else {
              if (args.Player.Name == MyClan.Owner)
                args.Player.SendErrorMessage("You are the owner of this clan, this means that if you leave, the clan will disband!");
              args.Player.SendInfoMessage("Are you sure you want to leave this clan? type \"/clan leave confirm\"");
            }
          }
          break;
        #endregion leave

        #region inviteMode
        case "invitemode":
          {
            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }
            if (MyClan.Owner != args.Player.Name) {
              args.Player.SendErrorMessage("You are not allowed to alter the clan's invitemode settings!");
              return;
            }
            string subcmd = args.Parameters.Count == 2 ? args.Parameters[1].ToLower() : string.Empty;
            switch (subcmd) {
              case "true":
                ClanManager.SetInviteMode(MyClan, InviteMode.True);
                break;
              case "false":
                ClanManager.SetInviteMode(MyClan, InviteMode.False);
                break;
              default:
                args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan invitemode <true/false>");
                return;
            }
            args.Player.SendInfoMessage("Clan invite mode has been set to " + (MyClan.InviteMode == InviteMode.True ? "true" : "false"));
          }
          break;
        #endregion inviteMode

        #region reloadclans
        case "reloadclans":
          {
            if (!args.Player.Group.HasPermission(Permission.Reload)) {
              args.Player.SendErrorMessage("You do not have permission to create a clan!");
              return;
            }
            ClanManager.ReloadAll();
            args.Player.SendInfoMessage("All clans and their members have been reloaded!");
            break;
          }
        #endregion reloadclans

        #region reloadconfig
        case "reloadconfig":
          {
            if (!args.Player.Group.HasPermission(Permission.Reload)) {
              args.Player.SendErrorMessage("You do not have permission to create a clan!");
              return;
            }
            ClanManager.ReloadConfig(args.Player);
          }
          break;
        #endregion reloadconfig

        #region list
        case "list":
          {
            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
              return;

            IEnumerable<string> clannames = ClanManager.Clans.Keys;
            List<string> output = new List<string>();
            foreach (string clanname in clannames) {
              Clan c = ClanManager.Clans[clanname];
              string temp = string.Format("{0,-31} - [{1,5}] - {2}", clanname, c.Tag, c.Owner);
              output.Add(temp);
            }
            PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(output),
                new PaginationTools.Settings {
                  HeaderFormat = "Clans ({0}/{1}):",
                  FooterFormat = "Type /clan list {0} for more.",
                  NothingToDisplayString = "There aren't any clans!",
                });
          }
          break;
        #endregion list

        #region tp
        case "tp":
        case "spawn":
        case "home":
          {
            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }
            if (MyClan.SpawnX == 0 || MyClan.SpawnY == 0) {
              args.Player.SendErrorMessage("Your clan has no spawn point defined!");
              return;
            }

            args.Player.Teleport(MyClan.SpawnX * 16, MyClan.SpawnY * 16);
          }
          break;
        #endregion tp

        #region setspawn
        case "setspawn":
        case "spawnpoint":
        case "settp":
        case "sethome":
          {
            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }
            if (MyClan.Owner != args.Player.Name) {
              args.Player.SendErrorMessage("You are not allowed to alter the clan's spawnpoint!");
              return;
            }

            int x = args.Player.TileX;
            int y = args.Player.TileY;
            x = Math.Max(0, x);
            y = Math.Max(0, y);
            x = Math.Min(x, Main.maxTilesX - 1);
            y = Math.Min(y, Main.maxTilesY - 1);

            ClanManager.SetSpawn(MyClan, x, y);
            args.Player.SendInfoMessage(string.Format("Your clan's spawnpoint has been changed to X:{0}, Y:{1}", MyClan.SpawnX, MyClan.SpawnY));
          }
          break;
        #endregion setspawn

        #region setcolor
        case "setcolor":
          {
            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }
            if (MyClan.Owner != args.Player.Name) {
              args.Player.SendErrorMessage("You are not allowed to alter the clan's chatcolor!");
              return;
            }
            if (args.Parameters.Count < 2) {
              args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan setcolor  <0-255,0-255,0-255>");
              return;
            }
            if (!ClanManager.SetColor(MyClan, args.Parameters[1]))
              args.Player.SendErrorMessage("Invalid color format! proper example: /clan setcolor 125,255,137");
          }
          break;
        #endregion setcolor

        #region online
        case "online":
        case "who":
          {
            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }

            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
              return;
            IEnumerable<string> clanMembers = MyClan.OnlineClanMembers.Select(m => m.Name);
            PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(clanMembers),
                new PaginationTools.Settings {
                  HeaderFormat = clanMembers.Count<string>() + " Online Clanmembers (page: {0}/{1}):",
                  FooterFormat = "Type /clan who {0} for more.",
                });
          }
          break;
        #endregion online

        #region members
        case "members":
          {
            Clan toList = null;
            if (args.Parameters.Count == 1) {
              toList = MyClan;
              if (toList == null) {
                args.Player.SendErrorMessage("You are not in any clan!");
                return;
              }
            } else {
              toList = ClanManager.FindClanByName(args.Parameters[1]);
              if (toList == null) {
                args.Player.SendErrorMessage("Clan {0} does not exist.", args.Parameters[1]);
                return;
              }
            }
            
            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 2, args.Player, out pageNumber))
              return;

            PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(toList.ClanMembers),
                new PaginationTools.Settings {
                  HeaderFormat = toList.Name + ": " + toList.ClanMembers.Count<string>() + " Clanmembers (page: {0}/{1}):",
                  FooterFormat = "Type /clan members {0} for more.",
                });
          }
          break;
        #endregion online

        #region find
        case "find":
          {
            if (args.Parameters.Count < 2) {
              args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan find <player>");
              return;
            }

            User usr = TShock.Users.GetUserByName(args.Parameters[1]);
            if (usr == null) {
              args.Player.SendErrorMessage("Player Not Found.");
              return;
            }

            Clan c = ClanManager.FindClanByPlayer(usr.ID);
            if (c == null) {
              args.Player.SendErrorMessage(string.Format("{0} is not in a clan!", usr.Name));
              return;
            }
            args.Player.SendInfoMessage(string.Format("{0} is in clan: {1}", usr.Name, c.Name));
          }
          break;
        #endregion find

        #region togglechat
        case "togglechat":
          {
            ClanMember member = MyClan.OnlineClanMembers.Where(x => x.Index == args.Player.Index).FirstOrDefault();
            member.DefaultToClanChat = !member.DefaultToClanChat;
            member.Player.SendInfoMessage(member.DefaultToClanChat ?
              "You will now automaticly talk in the clanchat!" : "You are now using global chat, use /c to talk in clanchat");
          }
          break;
        #endregion togglechat

        #region rename
        case "rename":
          {
            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }

            if (args.Parameters.Count < 2) {
              args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan rename <clan name>");
              return;
            }

            if (MyClan.Owner != args.Player.Name) {
              args.Player.SendErrorMessage("You are not allowed to alter the clan's name!");
              return;
            }
            string name = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
            if (ClanManager.FindClanByName(name) != null) {
              args.Player.SendErrorMessage("A clan with this name already exists!");
              return;
            }

            if (name.Length >= ClanManager.Config.ClanNameLength) {
              args.Player.SendErrorMessage("Clan name is too long.");
              return;
            }

            ClanManager.Rename(MyClan, name);
            MyClan.Broadcast("Your clan has been renamed to " + MyClan.Name);
          }
          break;
        #endregion rename

        #region invite
        case "invite":
          {
            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }

            if (MyClan.Owner != args.Player.Name) {
              args.Player.SendErrorMessage("You are not allowed to invite people!");
              return;
            }

            if (args.Parameters.Count < 2) {
              args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan invite <player name>");
              return;
            }

            string playerName = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
            List<TSPlayer> tsplrs = TShock.Utils.FindPlayer(playerName);
            if (tsplrs.Count > 1) {
              TShock.Utils.SendMultipleMatchError(args.Player, tsplrs.Select(p => p.Name));
              return;
            }

            if (!tsplrs[0].IsLoggedIn) {
              args.Player.SendErrorMessage("Player is not logged in!");
              return;
            }

            if (ClanManager.FindClanByPlayer(tsplrs[0]) != null) {
              args.Player.SendErrorMessage("Player is already in a clan!");
              return;
            }

            if (MyClan.IsBanned(tsplrs[0].Name)) {
              args.Player.SendErrorMessage("Player \"{0}\" is banned from this clan and cannot be invited!", tsplrs[0].Name);
              return;
            }

            if (ClanManager.PendingInvites[tsplrs[0].Index].Timeout > 0) {
              args.Player.SendErrorMessage("Player already has an invite pending.");
              return;
            }

            ClanManager.PendingInvites[tsplrs[0].Index] = new ClanInvite(MyClan.Name);
            args.Player.SendInfoMessage("Invite sent");
          }
          break;
        #endregion invite

        #region acceptinvite
        case "accept":
        case "ai":
        case "acceptinvite":
          {
            if (!args.Player.IsLoggedIn) {
              args.Player.SendErrorMessage("You need to be logged in to use this command!");
              return;
            }

            if (ClanManager.PendingInvites[args.Player.Index].Timeout <= 0) {
              args.Player.SendErrorMessage("You do not have any invites pending");
              return;
            }

            ClanManager.PendingInvites[args.Player.Index].Timeout = 0;
            Clan c = ClanManager.FindClanByName(ClanManager.PendingInvites[args.Player.Index].InvitingClan);
            ClanManager.JoinClan(c, new ClanMember(args.Player.User.ID, args.Player.Name, c.Name, args.Player.Index));
          }
          break;
        #endregion

        #region denyinvite
        case "deny":
        case "di":
        case "denyinvite":
          {
            if (!args.Player.IsLoggedIn) {
              args.Player.SendErrorMessage("You need to be logged in to use this command!");
              return;
            }

            if (ClanManager.PendingInvites[args.Player.Index].Timeout <= 0) {
              args.Player.SendErrorMessage("You do not have any invites pending");
              return;
            }

            ClanManager.PendingInvites[args.Player.Index].Timeout = 0;
            Clan c = ClanManager.FindClanByName(ClanManager.PendingInvites[args.Player.Index].InvitingClan);
            c.Broadcast(string.Format("{0} has denied your clan's invitation.", args.Player.Name));
            args.Player.SendInfoMessage("You have denied the clan's invitation.");
          }
          break;
        #endregion

        #region ban
        case "ban":
          {
            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }

            if (args.Player.Name != MyClan.Owner) {
              args.Player.SendErrorMessage("You are not allowed to ban members!");
              return;
            }

            if (args.Parameters.Count != 2 || args.Parameters[1] == "help") {
              args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan ban <member name>");
              return;
            }

            if (args.Parameters[1] == MyClan.Owner) {
              args.Player.SendErrorMessage("You are not allowed to ban the clan owner.");
              return;
            }

            User user = TShock.Users.GetUserByName(args.Parameters[1]);
            if (user == null) {
              args.Player.SendErrorMessage("Player not found");
              return;
            }
            
            if (MyClan.ClanMembers.Contains(user.Name)) {
              ClanMember member = ClanManager.getClanMember(user.Name);
              if (ClanManager.InsertBan(MyClan, member))
                MyClan.Broadcast(string.Format("{0} is banned from your clan!", member.Name));
              else
                args.Player.SendErrorMessage("An error occured, contact an administrator for help");
            } else {
              if (ClanManager.InsertBan(MyClan, user.Name))
                MyClan.Broadcast(string.Format("{0} is banned from your clan!", user.Name));
              else
                args.Player.SendErrorMessage("An error occured, contact an administrator for help");
            }
          }
          break;
        #endregion

        #region unban
        case "unban":
          {
            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }

            if (args.Player.Name != MyClan.Owner) {
              args.Player.SendErrorMessage("You are not allowed to unban members!");
              return;
            }

            if (args.Parameters.Count != 2 || args.Parameters[1] == "help") {
              args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan unban <member name>");
              return;
            }

            User user = TShock.Users.GetUserByName(args.Parameters[1]);
            if (user == null) {
              args.Player.SendErrorMessage("Player not found.");
              return;
            }

            if (!MyClan.Bans.Contains(user.Name)) {
              args.Player.SendErrorMessage("Player is not banned.");
              return;
            }

            if (ClanManager.RemoveBan(MyClan, user.Name))
              MyClan.Broadcast(string.Format("{0} is unbanned from your clan!", user.Name));
            else
              args.Player.SendErrorMessage("An error occured, contact an administrator for help");
          }
          break;
        #endregion

        #region kick
        case "kick":
          {
            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }

            if (args.Player.Name != MyClan.Owner) {
              args.Player.SendErrorMessage("You are not allowed to kick members!");
              return;
            }

            if (args.Parameters.Count != 2 || args.Parameters[1] == "help") {
              args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan kick <member name>");
              return;
            }

            if (args.Parameters[1] == MyClan.Owner) {
              args.Player.SendErrorMessage("You are not allowed to kick the clan owner.");
              return;
            }

            if (!MyClan.ClanMembers.Contains(args.Parameters[1])) {
              args.Player.SendErrorMessage("Player not found or player is not a member of your clan!");
              return;
            }

            ClanMember member = ClanManager.ClanMembers.Values.Where(x => x.Name == args.Parameters[1]).First();
            ClanManager.LeaveClan(MyClan, member);
            MyClan.Broadcast(string.Format("{0} is kicked from your clan!", member.Name));
          }
          break;
        #endregion

        #region help
        default:
        case "help":
          {
            int pageNumber;
            int pageParamIndex = 1;
            if (args.Parameters.Count > 2)
              pageParamIndex = 2;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, pageParamIndex, args.Player, out pageNumber))
              return;

            List<string> lines = new List<string>();
            if (MyClan != null && args.Player.Group.HasPermission(Permission.Chat))
              lines.Add("/c <message> - talk in your clan's chat.");

            if (MyClan == null && args.Player.Group.HasPermission(Permission.Create))
              lines.Add("/clan create <clanname> - create a new clan with you as leader.");
            
            if (MyClan == null)
              lines.Add("/clan join <name> - join an existing clan.");

            // General Commands
            if (args.Player.Group.HasPermission(Permission.Use)) {
              lines.Add("/clan list - list all existing clans.");
              lines.Add("/clan find <player> - find out which clan a player is in.");
              lines.Add("/clan accept|acceptinvite|ai - join a clan you were invited to.");
              lines.Add("/clan deny|denyinvite|ai - deny a pending clan invitation.");
            }

            // General Clan member commands
            if (MyClan != null && args.Player.Group.HasPermission(Permission.Use)) {
              lines.Add("/clan tp|spawn|home - teleports to clan's spawnpoint");
              lines.Add("/clan who|online - list all online members in your clan.");
              lines.Add("/clan members [clan name]- list all members in your clan, or clan name specified.");
              lines.Add("/clan leave - leave your current clan.");
              lines.Add("/clan togglechat - toggle auto-talking in clanchat instead of global chat.");
              lines.Add("/clan kick <player> - will kick a player out of your clan.");
            }

            // Owner only commands
            if (MyClan != null && MyClan.Owner == args.Player.Name) {
              lines.Add("/clan settp|setspawn|sethome - sets the clan's spawnpoint to current position");
              lines.Add("/clan invite <name> - will invite a player to your clan.");
              lines.Add("/clan invitemode <true/false> - toggle invite-only mode.");
              lines.Add("/clan rename <new name> - change your clan's name.");
              lines.Add("/clan tag <tag> - create a new clan tag.");
              lines.Add("/clan ban <player> - will ban a player from your clan.");
              lines.Add("/clan unban <player> - will unban a player from your clan.");
              lines.Add("/clan setcolor <r, g, b> - change the clanchat's color.");
            }

            // Admin commands
            if (args.Player.Group.HasPermission(Permission.Reload)) {
              lines.Add("/clan reloadclans - reload all clans and their members.");
              lines.Add("/clan reloadconfig - reload the clans configuration file.");
            }

            PaginationTools.SendPage(
              args.Player, pageNumber, lines,
              new PaginationTools.Settings {
                HeaderFormat = "Available Clan Sub-Commands ({0}/{1}):",
                FooterFormat = "Type /clan help {0} for more sub-commands."
              }
            );

            break;
          }
          #endregion help
      }
    }
    #endregion

    #region Plugin Hooks
    void ClanHooks_ClanCreated(ClanCreatedEventArgs e) {
      e.Member.Player.SendSuccessMessage(string.Format("Your clan ({0}) has been successfully created!", e.ClanName));
      TSPlayer.All.SendInfoMessage(string.Format("{0} has created a new clan: {1}.", e.Member.Name, e.ClanName));
    }

    void ClanHooks_ClanLogin(ClanLoginEventArgs e) {
      e.Clan.Broadcast(e.Member.Name + " has entered the clan!", e.Member.Index);
    }

    void ClanHooks_ClanJoin(ClanJoinEventArgs e) {
      e.Clan.Broadcast(string.Format("A new member ({0}) has joined the clan!", e.Member.Name), e.Member.Index);
      e.Member.Player.SendInfoMessage("Welcome to the clan!");
    }

    void ClanHooks_ClanRemoved(ClanRemovedEventArgs e) {
      e.Clan.Broadcast("The clan has been disbanded!");
      TSPlayer.All.SendInfoMessage(string.Format("Clan {0} has been disbanded!", e.Clan.Name));
    }

    void ClanHooks_ClanLeave(ClanLeaveEventArgs e) {
      e.Clan.Broadcast(e.Member.Name + " has left the clan!", e.Member.Index);
      if (e.Member.Index > -1)
        e.Member.Player.SendInfoMessage("You have left the clan!");
    }
    #endregion
  }
}
