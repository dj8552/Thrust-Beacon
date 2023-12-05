using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.IO;
using VRage.Utils;

namespace ThrustBeacon
{
    [ProtoContract]
    public class ServerSettings
    {
        public static ServerSettings Instance;
        public static readonly ServerSettings Default = new ServerSettings()
        {
            IncludePowerInSignal = true,
            DefaultPowerDivisor = 600, //Default power divisor if no other value is specified
            IncludeThrustInSignal = true,
            DefaultThrustDivisor = 600, //Default thrust divisor if no other value is specified
            LargeGridCooldownRate = 0.95f, //Previous signal is multiplied by this per 59 tick cycle (unless freshly calc'd value is > than old value)
            SmallGridCooldownRate = 0.85f, //Same as above
            ShutdownPowerOverMaxSignal = true,
            MaxSignalforPowerShutdown = 500000,
            ShutdownThrustersOverMaxSignal = true,
            MaxSignalforThrusterShutdown = 500000,
            IncludeWeaponHeatInSignal = true,
            DefaultWeaponHeatDivisor = 1, //Explore further and find a good starting point for this value
            SendSignalDataToSuits = false, //If false, characters outside of grids will not get beacon updates
            IncludeShieldHPInSignal = true,
            DefaultShieldHPDivisor = 50,
        };

        [ProtoMember(1)]
        public bool IncludePowerInSignal { get; set; }

        [ProtoMember(2)]
        public int DefaultPowerDivisor { get; set; }

        [ProtoMember(3)]
        public bool IncludeThrustInSignal { get; set; }

        [ProtoMember(4)]
        public int DefaultThrustDivisor { get; set; }

        [ProtoMember(5)]
        public float LargeGridCooldownRate { get; set; }

        [ProtoMember(6)]
        public float SmallGridCooldownRate { get; set; }

        [ProtoMember(7)]
        public bool ShutdownPowerOverMaxSignal { get; set; }

        [ProtoMember(8)]
        public double MaxSignalforPowerShutdown { get; set; }

        [ProtoMember(9)]
        public bool ShutdownThrustersOverMaxSignal { get; set; }

        [ProtoMember(10)]
        public double MaxSignalforThrusterShutdown { get; set; }

        [ProtoMember(11)]
        public bool IncludeWeaponHeatInSignal { get; set; }
        
        [ProtoMember(12)]
        public double DefaultWeaponHeatDivisor { get; set; }

        [ProtoMember(13)]
        public bool SendSignalDataToSuits { get; set; }

        [ProtoMember(14)]
        public bool IncludeShieldHPInSignal { get; set; }
        [ProtoMember(15)]
        public int DefaultShieldHPDivisor { get; set; }
    }
    public partial class Session
    {
        private void InitServerConfig()
        {
            ServerSettings s = ServerSettings.Default;
            var Filename = "ServerConfig.cfg";
            var localFileExists = MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(ServerSettings));
            if(localFileExists)
            {
                TextReader reader = null;
                try
                {
                    reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(ServerSettings));
                    string text = reader.ReadToEnd();
                    reader.Close();
                    s = MyAPIGateway.Utilities.SerializeFromXML<ServerSettings>(text);
                    ServerSettings.Instance = s;
                    MyLog.Default.WriteLineAndConsole(ModName + "Loaded server config");
                }
                catch (Exception e)
                {
                    if (reader != null) reader.Close();
                    MyLog.Default.WriteLineAndConsole(ModName + "Server config read error, writing default file - " + e.InnerException);
                    s = ServerSettings.Default;
                    SaveServer(s);
                }
            }
            else
            {
                s = ServerSettings.Default;
                SaveServer(s);
            }
        }
        public void SaveServer(ServerSettings settings)
        {
            var Filename = "ServerConfig.cfg";
            TextWriter writer;
            writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(ServerSettings));
            writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
            writer.Close();
            ServerSettings.Instance = settings;
            MyLog.Default.WriteLineAndConsole(ModName + "Saved server config sample");

        }
    }
}

