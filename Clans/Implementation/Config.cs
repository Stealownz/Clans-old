using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using TShockAPI;

namespace Clans {
  public class Config {
    public static string ConfigPath = Path.Combine(TShock.SavePath, "Clans", "ClanConfig.json");

    public int MaxNumberOfClans { get; set; }
    public string DefaultChatColor { get; set; }
    public int ClanNameLength { get; set; }
    public int ClanTagLength { get; set; }

    public Config() {
      MaxNumberOfClans = 0;
      DefaultChatColor = "135,214,9";
      ClanNameLength = 30;
      ClanTagLength = 5;
    }

    public Color ParseColor() {
      int r, g, b;
      string[] s = DefaultChatColor.Split(',');
      if (s.Length == 3) {
        if (int.TryParse(s[0], out r) && int.TryParse(s[1], out g) && int.TryParse(s[2], out b))
          return new Color(r, g, b);
      }
      return new Color(135, 214, 9);
    }

    public Config Read() {
      if (!File.Exists(ConfigPath)) {
        write();
        return new Config();
      }

      try {
        return JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigPath));
      }
      catch (Exception ex) {
        TShock.Log.Error("[Clans] an error has occurred while reading the config file! See below for more info:");
        TShock.Log.Error(ex.ToString());
        return new Config();
      }
    }

    public void write() {
      File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
    }
  }
}
