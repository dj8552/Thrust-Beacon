using Sandbox.ModAPI;
using System.IO;
using VRage.Utils;
using System;

namespace ThrustBeacon
{
    public partial class Session
    {
        private void InitDefaults()
        {
            Settings s = Settings.Default;
            var Filename = "Config.cfg";
            var localFileExists = MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(Settings));
            if(localFileExists)
            {
                try
                {
                    TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(Settings));
                    string text = reader.ReadToEnd();
                    reader.Close();
                    s = MyAPIGateway.Utilities.SerializeFromXML<Settings>(text);
                    Settings.Instance = s;
                    MyLog.Default.WriteLineAndConsole(ModName + "Initialized default client config");
                    serverDefaults = true;
                }
                catch (Exception e)
                {
                    MyLog.Default.WriteLineAndConsole(ModName + "Error reading client default config file - no default overrides will be sent" + e.InnerException);
                }
            }
            else
                MyLog.Default.WriteLineAndConsole(ModName + "no default client config found");

        }
    }
}

