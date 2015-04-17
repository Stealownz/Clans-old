using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Net;
using System.Reflection;

namespace Clans
{
    public class UpdateChecker
    {
        private string _newVersion;
        public string NewVersion { get { return _newVersion; } }
        private bool _updateAvailable;
        public bool UpdateAvailable { get { return _updateAvailable; } }

        private string[] _changeLog;

        public string[] ChangeLog { get { return _changeLog; } }

        private Timer _timer = new Timer(1000 * 60) { Enabled=true };
        public UpdateChecker()
        {
            _timer.Elapsed += _timer_Elapsed;
        }

        ~UpdateChecker()
        {
            _timer.Elapsed -= _timer_Elapsed;
        }

        void _timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!UpdateAvailable)
                CheckForUpdate();
        }

        public void CheckForUpdate()
        {
            try
            {
                WebClient wc = new WebClient() { Proxy = null };

                string[] msg = wc.DownloadString("https://raw.githubusercontent.com/ancientgods/Clans/master/Update").Split('\n');

                if (msg.Length <= 0)
                    return;

                int newversion, currentversion;
                string CurrVStr, NewVStr;

                CurrVStr = Assembly.GetExecutingAssembly().GetName().Version.ToString().Replace(".", "");
                _newVersion = msg[0];
                NewVStr = _newVersion.Replace(".", "");

                if (int.TryParse(CurrVStr, out currentversion) && int.TryParse(NewVStr, out newversion))
                {
                    if (newversion > currentversion)
                        _updateAvailable = true;

                    string[] res = new string[msg.Length - 1];
                    for (int i = 1; i < msg.Length; i++)
                        res[i - 1] = msg[i];

                    _changeLog = res;
                }
            }
            catch {  }
        }
    }
}
