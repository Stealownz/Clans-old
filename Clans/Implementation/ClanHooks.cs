using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TShockAPI;

namespace Clans.Hooks {
  public static class ClanHooks {
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

    public static void OnClanCreated(ClanMember member, string clanname) {
      if (ClanCreated == null)
        return;

      ClanCreated(new ClanCreatedEventArgs() { Member = member, ClanName = clanname });
    }

    public static void OnClanRemoved(Clan clan) {
      if (ClanRemoved == null)
        return;

      ClanRemoved(new ClanRemovedEventArgs() { Clan = clan });
    }

    public static void OnClanLogin(ClanMember member, Clan clan) {
      if (ClanLogin == null)
        return;

      ClanLogin(new ClanLoginEventArgs() { Member = member, Clan = clan });
    }

    public static void OnClanLogout(ClanMember member, Clan clan) {
      if (ClanLogout == null)
        return;

      ClanLogout(new ClanLogoutEventArgs() { Member = member, Clan = clan });
    }

    public static void OnClanJoin(ClanMember member, Clan clan) {
      if (ClanJoin == null)
        return;

      ClanJoin(new ClanJoinEventArgs() { Member = member, Clan = clan });
    }

    public static void OnClanLeave(ClanMember member, Clan clan) {
      if (ClanLeave == null)
        return;

      ClanLeave(new ClanLeaveEventArgs() { Member = member, Clan = clan });
    }
  }

  public class ClanCreatedEventArgs : EventArgs {
    public ClanMember Member;
    public string ClanName;
  }

  public class ClanRemovedEventArgs : EventArgs {
    public Clan Clan;
  }

  public class ClanLoginEventArgs : EventArgs {
    public ClanMember Member;
    public Clan Clan;
  }

  public class ClanLogoutEventArgs : EventArgs {
    public ClanMember Member;
    public Clan Clan;
  }

  public class ClanJoinEventArgs : EventArgs {
    public ClanMember Member;
    public Clan Clan;
  }

  public class ClanLeaveEventArgs : EventArgs {
    public ClanMember Member;
    public Clan Clan;
  }
}
