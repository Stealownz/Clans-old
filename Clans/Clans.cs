using System.Collections.Generic;
using TerrariaApi.Server;
using System.Reflection;
using TShockAPI.Hooks;
using System.Linq;
using System.Text;
using Clans.Hooks;
using TShockAPI;
using Terraria;
using System;

namespace Clans {
  [ApiVersion(1, 20)]
  public class Clans : TerrariaPlugin {
    public override Version Version {
      get { return Assembly.GetExecutingAssembly().GetName().Version; }
    }
    public override string Author {
      get { return "Ancientgods"; }
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

      ServerApi.Hooks.ServerChat.Register(this, OnChat);
      ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
      TShockAPI.Hooks.PlayerHooks.PlayerPostLogin += new TShockAPI.Hooks.PlayerHooks.PlayerPostLoginD(PlayerHooks_PlayerPostLogin);

      Commands.ChatCommands.Add(new Command(Permission.Use, ClanCmd, "clan"));
      Commands.ChatCommands.Add(new Command(Permission.Chat, Chat, "c") { AllowServer = false, DoLog = false });

      ClanManager.Initialize();
    }

    protected override void Dispose(bool disposing) {
      if (disposing) {
      }
      base.Dispose(disposing);
    }

    public Clans(Main game)
        : base(game) {
      Order = 1;
    }

    void PlayerHooks_PlayerPostLogin(PlayerPostLoginEventArgs e) {
      if (ClanManager.ClanMembers.ContainsKey(e.Player.Index))
        ClanManager.UnLoadMember(e.Player);
      ClanManager.LoadMember(e.Player);
    }

    void OnLeave(LeaveEventArgs e) {
      ClanManager.UnLoadMember(TShock.Players[e.Who]);
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
        Myclan.OnlineClanMembers[ts.Index].DefaultToClanChat = false;
        return;
      }

      if (Myclan.OnlineClanMembers[ts.Index].DefaultToClanChat) {
        args.Handled = true;
        string msg = string.Format("[Clanchat] {0} - {1}: {2}", Myclan.Tag, ts.Name, string.Join(" ", args.Text));
        Myclan.Broadcast(msg);
        TShock.Utils.SendLogs(msg, Color.PaleVioletRed);
      }
    }

    static string[] HelpMsg = new string[]
    {
      /*
      // TO TEST:      
      
      // Future Plans
      Wormhole potion 
      Add external admin commands.
      */

      "/c <message> - talk in your clan's chat.",
      "/clan create <name> - create a new clan with you as leader.",
      "/clan tag <tag> - create a new clan tag.",
      "/clan join <name> - join an existing clan.",
      "/clan leave - leave your current clan.",
      "/clan reloadclans - reload all clans and their members.",
      "/clan reloadconfig - reload the clans configuration file.",
      "/clan invitemode <true/false> - toggle invite-only mode.",
      "/clan list - list all existing clans.",
      //"/clan tp - teleport to the clan's spawnpoint.",
      //"/clan setspawn - set the clan's spawnpoint to your current location.",
      "/clan setcolor <r, g, b> - change the clanchat's color.",
      "/clan who - list all online members in your clan.",
      "/clan find <player> - find out which clan a player is in.",
      "/clan togglechat - toggle auto-talking in clanchat instead of global chat.",
      "/clan rename <new name> - change your clan's name.",
      "/clan invite <name> - will invite a player to your clan.",
      "/clan acceptinvite - join a clan you were invited to.",
      "/clan denyinvite - deny a pending clan invitation.",
      "/clan ban <player> - will ban a player from your clan.",
      "/clan unban <player> - will unban a player from your clan.",
      "/clan kick <player> - will kick a player out of your clan.",
      /*
      "/clan tpall - teleport all clan members to you.",
      */
    };

    void Chat(CommandArgs args) {
      Clan Myclan = ClanManager.FindClanByPlayer(args.Player);
      if (Myclan == null) {
        args.Player.SendErrorMessage("You are not in a clan!");
        return;
      }
      string msg = string.Format("[Clanchat] {0} - {1}: {2}", Myclan.Tag, args.Player.Name, string.Join(" ", args.Parameters));
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
            if (!ClanManager.CreateClan(args.Player, new Clan() { Name = name, Owner = args.Player.User.Name }))
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

            if (MyClan.Owner != args.Player.User.Name) {
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
            ClanManager.UpdateTag(MyClan, "{" + args.Parameters[1] + "}");
            MyClan.Broadcast("Clan Tag updated.");
          }
          break;
        #endregion

        #region join
        case "join":
          {
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
            ClanManager.JoinClan(args.Player, c);
          }
          break;
        #endregion join

        #region leave
        case "leave":
          {
            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }
            if (args.Parameters.Count == 2) {
              if (args.Parameters[1].ToLower() == "confirm")
                ClanManager.LeaveClan(args.Player, MyClan);
              else
                args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan leave confirm");
            }
            else {
              if (args.Player.User.Name == MyClan.Owner)
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
            if (MyClan.Owner != args.Player.User.Name) {
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
            IEnumerable<string> clanNames = ClanManager.Clans.Keys;
            PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(clanNames),
                new PaginationTools.Settings {
                  HeaderFormat = "Clans ({0}/{1}):",
                  FooterFormat = "Type /clan list {0} for more.",
                  NothingToDisplayString = "There aren't any clans!",
                });
          }
          break;
        #endregion list

        #region //tp
        //case "tp":
        //  {
        //    if (MyClan == null) {
        //      args.Player.SendErrorMessage("You are not in a clan!");
        //      return;
        //    }
        //    if (MyClan.TileX == 0 || MyClan.TileY == 0) {
        //      args.Player.SendErrorMessage("Your clan has no spawn point defined!");
        //      return;
        //    }
        //    args.Player.Teleport(MyClan.TileX * 16, MyClan.TileY * 16);
        //  }
        //  break;
        #endregion tp

        #region //setspawn
        //case "setspawn":
        //  {
        //    if (MyClan == null) {
        //      args.Player.SendErrorMessage("You are not in a clan!");
        //      return;
        //    }
        //    if (MyClan.Owner != args.Player.User.Name) {
        //      args.Player.SendErrorMessage("You are not allowed to alter the clan's spawnpoint!");
        //      return;
        //    }
        //    ClanManager.SetSpawn(MyClan, args.Player);
        //    args.Player.SendInfoMessage(string.Format("Your clan's spawnpoint has been changed to X:{0}, Y:{1}", MyClan.TileX, MyClan.TileY));
        //  }
          break;
        #endregion setspawn

        #region setcolor
        case "setcolor":
          {
            if (MyClan == null) {
              args.Player.SendErrorMessage("You are not in a clan!");
              return;
            }
            if (MyClan.Owner != args.Player.User.Name) {
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

        #region who
        case "who":
          {
            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
              return;
            IEnumerable<string> clanMembers = MyClan.OnlineClanMembers.Values.Select(m => m.TSPlayer.Name);
            PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(clanMembers),
                new PaginationTools.Settings {
                  HeaderFormat = clanMembers.Count<string>() + " Online Clanmembers (page: {0}/{1}):",
                  FooterFormat = "Type /clan who {0} for more.",
                });
          }
          break;
        #endregion who

        #region find
        case "find":
          {
            if (args.Parameters.Count < 2) {
              args.Player.SendErrorMessage("Invalid syntax! proper syntax: /clan find <player>");
              return;
            }
            var foundplr = TShock.Utils.FindPlayer(args.Parameters[1]);
            if (foundplr.Count == 0) {
              args.Player.SendMessage("Invalid player!", Color.Red);
              return;
            }
            else if (foundplr.Count > 1) {
              args.Player.SendMessage(string.Format("More than one ({0}) player matched!", foundplr.Count), Color.Red);
              return;
            }
            TSPlayer plr = foundplr[0];
            Clan c = ClanManager.FindClanByPlayer(plr);
            if (c == null) {
              args.Player.SendErrorMessage(string.Format("{0} is not in a clan!", plr.Name));
              return;
            }
            args.Player.SendInfoMessage(string.Format("{0} is in clan: {1}", plr.Name, c.Name));
          }
          break;
        #endregion find

        #region togglechat
        case "togglechat":
          {
            MyClan.OnlineClanMembers[args.Player.Index].DefaultToClanChat = !MyClan.OnlineClanMembers[args.Player.Index].DefaultToClanChat;
            args.Player.SendInfoMessage(MyClan.OnlineClanMembers[args.Player.Index].DefaultToClanChat ? "You will now automaticly talk in the clanchat!" : "You are now using global chat, use /c to talk in clanchat");
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
            if (MyClan.Owner != args.Player.User.Name) {
              args.Player.SendErrorMessage("You are not allowed to alter the clan's name!");
              return;
            }
            string name = String.Join(" ", args.Parameters.GetRange(1, args.Parameters.Count - 1));
            if (ClanManager.FindClanByName(name) != null) {
              args.Player.SendErrorMessage("A clan with this name already exists!");
              return;
            }
            ClanManager.Rename(MyClan, args.Player, name);
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

            if (MyClan.Owner != args.Player.User.Name) {
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
        case "acceptinvite":
          {
            if (ClanManager.PendingInvites[args.Player.Index].Timeout <= 0) {
              args.Player.SendErrorMessage("You do not have any invites pending");
              return;
            }

            ClanManager.PendingInvites[args.Player.Index].Timeout = 0;
            Clan c = ClanManager.FindClanByName(ClanManager.PendingInvites[args.Player.Index].InvitingClan);
            ClanManager.JoinClan(args.Player, c);
          }
          break;
        #endregion

        #region denyinvite
        case "denyinvite":
          {
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

            List<TSPlayer> tsplrs = TShock.Utils.FindPlayer(args.Parameters[1]);

            if (tsplrs.Count > 1) {
              TShock.Utils.SendMultipleMatchError(args.Player, tsplrs.Select(p => p.Name));
              return;
            }

            Clan clan = ClanManager.FindClanByPlayer(tsplrs[0]);
            if (clan.Name != MyClan.Name) {
              args.Player.SendErrorMessage("Player is not in your clan!");
              return;
            }

            if (ClanManager.InsertBan(MyClan, tsplrs[0]))
              MyClan.Broadcast(string.Format("{0} is banned from your clan!", tsplrs[0].Name));
            else
              args.Player.SendErrorMessage("An error occured, contact an administrator for help");
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

            List<TSPlayer> tsplrs = TShock.Utils.FindPlayer(args.Parameters[1]);

            if (tsplrs.Count > 1) {
              TShock.Utils.SendMultipleMatchError(args.Player, tsplrs.Select(p => p.Name));
              return;
            }

            if (ClanManager.RemoveBan(MyClan, tsplrs[0]))
              MyClan.Broadcast(string.Format("{0} is unbanned from your clan!", tsplrs[0].Name));
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

            List<TSPlayer> tsplrs = TShock.Utils.FindPlayer(args.Parameters[1]);

            if (tsplrs.Count > 1) {
              TShock.Utils.SendMultipleMatchError(args.Player, tsplrs.Select(p => p.Name));
              return;
            }

            ClanManager.LeaveClan(tsplrs[0], MyClan);
            MyClan.Broadcast(string.Format("{0} is kicked from your clan!", tsplrs[0].Name));
          }
          break;
        #endregion

        #region help
        default:
        case "help":
          {
            int pageNumber;
            if (!PaginationTools.TryParsePageNumber(args.Parameters, 1, args.Player, out pageNumber))
              return;

            PaginationTools.SendPage(args.Player, pageNumber, PaginationTools.BuildLinesFromTerms(HelpMsg),
                new PaginationTools.Settings {
                  HeaderFormat = "Clans help page ({0}/{1})",
                  FooterFormat = "Type /clan help {0} for more.",
                });
          }
          break;
          #endregion help
      }
    }

    void ClanHooks_ClanCreated(ClanCreatedEventArgs e) {
      e.TSplayer.SendSuccessMessage(string.Format("Your clan ({0}) has been successfully created!", e.ClanName));
      TSPlayer.All.SendInfoMessage(string.Format("{0} has created a new clan: {1}.", e.TSplayer.Name, e.ClanName));
    }

    void ClanHooks_ClanLogin(ClanLoginEventArgs e) {
      e.Clan.Broadcast(e.TSplayer.Name + " has entered the clan!", e.TSplayer.Index);
    }

    void ClanHooks_ClanJoin(ClanJoinEventArgs e) {
      e.Clan.Broadcast(string.Format("A new member ({0}) has joined the clan!", e.TSplayer.Name), e.TSplayer.Index);
      e.TSplayer.SendInfoMessage("Welcome to the clan!");
    }

    void ClanHooks_ClanRemoved(ClanRemovedEventArgs e) {
      e.Clan.Broadcast("The clan has been disbanded!");
      TSPlayer.All.SendInfoMessage(string.Format("Clan {0} has been disbanded!", e.Clan.Name));
    }

    void ClanHooks_ClanLeave(ClanLeaveEventArgs e) {
      e.Clan.Broadcast(e.TSplayer.Name + " has left the clan!", e.TSplayer.Index);
      e.TSplayer.SendInfoMessage("You have left the clan!");
    }
  }
}
