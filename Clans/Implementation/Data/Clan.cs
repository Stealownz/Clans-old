using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Clans {
  public class Clan {
    public string Name { get; set; }
    public string Tag { get; set; }
    public string Owner { get; set; }
    public InviteMode InviteMode { get; set; }
    public int TileX { get; set; }
    public int TileY { get; set; }
    public Color Color { get; set; }
    public List<string> ClanMembers {
      get {
        return ClanManager.ClanMembers.Where(c => c.Value.ClanName == this.Name).Select(x => x.Value.Name).ToList();
      }
    }
    public List<ClanMember> OnlineClanMembers {
      get {
        return ClanManager.ClanMembers.Values.Where(c => c.ClanName == this.Name && c.Index >= 0).ToList();
      }
    }
    public List<string> Bans { get; set; }

    public Clan(string name, string owner) {
      Name = name;
      Owner = owner;
      Bans = new List<string>();
      Color = ClanManager.Config.ParseColor();
      Tag = "";
    }

    public bool IsInClan(string playerName) {
      return ClanMembers.Contains(playerName);
    }

    public bool IsBanned(string name) {
      return Bans.Contains(name);
    }

    public void Broadcast(string msg, int ExcludePlayer = -1) {
      foreach (ClanMember member in OnlineClanMembers) {
        if (ExcludePlayer > -1 && member.Index == ExcludePlayer)
          continue;

        member.Player.SendMessage(msg, Color);
      }
    }
  }
}
