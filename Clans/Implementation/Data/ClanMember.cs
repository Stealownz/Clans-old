using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using TShockAPI.DB;
using TShockAPI;

namespace Clans {
  public class ClanMember {
    public int UserID { get; set; }
    public string Name { get; set; }
    public string ClanName { get; set; }
    public int Index { get; set; }
    public TSPlayer Player {
      get {
        if (Index >= 0)
          return TShock.Players[Index];
        else
          return null;
      }
    }
    public bool DefaultToClanChat { get; set; }

    public ClanMember(int userID, string playerName, string clanName, int index = -1) {
      UserID = userID;
      Name = playerName;
      ClanName = clanName;
      Index = index;
    }
  }
}
