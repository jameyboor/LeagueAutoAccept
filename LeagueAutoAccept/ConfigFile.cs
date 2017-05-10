using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeagueAutoAccept
{
    public class ConfigFile
    {
        public ConfigFile()
        {

        }

        public List<string> Champions { get; set; } = new List<string>();
        public string LeagueFolder;
        public string CurrentChampion;
        public int ScreenCaptureWaitTime;
        public int CheckQueueTimeout; 
    }
}
