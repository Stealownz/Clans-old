using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;

namespace Clans.Hooks
{
    public static class ClanHooks
    {
        public delegate void ClanCreatedD(ClanCreatedEventArgs e);
        public static event ClanCreatedD ClanCreated;

        public delegate void ClanRemovedD(ClanRemovedEventArgs e);
        public static event ClanRemovedD ClanRemoved;

        public delegate void ClanLoginD(ClanLoginEventArgs e);
        public static event ClanLoginD ClanLogin;

        public delegate void ClanLogoutD(ClanLogoutEventArgs e);
        public static event ClanLogoutD ClanLogout;

        public delegate void ClanJoinD(ClanJoinEventArgs e);
        public static event ClanJoinD ClanJoin;

        public delegate void ClanLeaveD(ClanLeaveEventArgs e);
        public static event ClanLeaveD ClanLeave;

        public static void OnClanCreated(TSPlayer ts, string clanname)
        {
            if (ClanCreated == null)
                return;

            ClanCreated(new ClanCreatedEventArgs() { TSplayer = ts, ClanName = clanname });
        }

        public static void OnClanRemoved(Clan clan)
        {
            if (ClanRemoved == null)
                return;

            ClanRemoved(new ClanRemovedEventArgs() { Clan = clan });
        }

        public static void OnClanLogin(TSPlayer ts, Clan clan)
        {
            if (ClanLogin == null)
                return;

            ClanLogin(new ClanLoginEventArgs() { TSplayer = ts, Clan = clan });
        }

        public static void OnClanLogout(TSPlayer ts, Clan clan)
        {
            if (ClanLogout == null)
                return;

            ClanLogout(new ClanLogoutEventArgs() { TSplayer = ts, Clan = clan });
        }

        public static void OnClanJoin(TSPlayer ts, Clan clan)
        {
            if (ClanJoin == null)
                return;

            ClanJoin(new ClanJoinEventArgs() { TSplayer = ts, Clan = clan });
        }

        public static void OnClanLeave(TSPlayer ts, Clan clan)
        {
            if (ClanLeave == null)
                return;

            ClanLeave(new ClanLeaveEventArgs() { TSplayer = ts, Clan = clan });
        }
    }

    public class ClanCreatedEventArgs : EventArgs
    {
        public TSPlayer TSplayer;
        public string ClanName;
    }

    public class ClanRemovedEventArgs : EventArgs
    {
        public TSPlayer TSplayer;
        public Clan Clan;
    }

    public class ClanLoginEventArgs : EventArgs
    {
        public TSPlayer TSplayer;
        public Clan Clan;
    }

    public class ClanLogoutEventArgs : EventArgs
    {
        public TSPlayer TSplayer;
        public Clan Clan;
    }

    public class ClanJoinEventArgs : EventArgs
    {
        public TSPlayer TSplayer;
        public Clan Clan;
    }

    public class ClanLeaveEventArgs : EventArgs
    {
        public TSPlayer TSplayer;
        public Clan Clan;
    }
}
