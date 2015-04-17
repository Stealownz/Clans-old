using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Newtonsoft.Json;
using TShockAPI;

namespace Clans
{
    public class Config
    {
        public static string ConfigPath = Path.Combine(TShock.SavePath, "Clans", "ClanConfig.json");

        public int MaxNumberOfClans { get; set; }
        public string DefaultChatColor { get; set; }

        public Config()
        {
            MaxNumberOfClans = 0;
            DefaultChatColor = "135,214,9";
        }

        public Color ParseColor()
        {
            int r, g, b;
            string[] s = DefaultChatColor.Split(',');
            if (s.Length == 3)
            {
                if (int.TryParse(s[0], out r) && int.TryParse(s[1], out g) && int.TryParse(s[2], out b))
                    return new Color(r, g, b);
            }
            return new Color(135, 214, 9);
        }

        public Config Read(TSPlayer ts = null)
        {
            if (!File.Exists(ConfigPath))
                write();

            try
            {
                Config res = JsonConvert.DeserializeObject<Config>(File.ReadAllText(ConfigPath));
                return res;
            }
            catch (Exception ex)
            {
                if (ts == null)
                {
                    Console.WriteLine("[Clans] an error has occurred while reading the config file! See below for more info:");
                    Console.WriteLine(ex.ToString());
                }
                else
                    ts.SendErrorMessage("[Clans] There was an error reloading the config file, check the console for more info!");
                return this;
            }
        }

        public void write()
        {
            File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
        }
    }
}
