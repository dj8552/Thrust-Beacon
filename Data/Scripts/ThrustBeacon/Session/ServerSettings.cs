using ProtoBuf;
using Draygo.API;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using VRage.Utils;
using VRageMath;

namespace ThrustBeacon
{
    [ProtoContract]
    public class ServerSettings
    {
        public static ServerSettings Instance;
        public static readonly ServerSettings Default = new ServerSettings()
        {
            IncludePowerInSignal = true,
            DefaultPowerDivisor = 600,
            IncludeThrustInSignal = true,
            DefaultThrustDivisor = 600,
            LargeGridCooldownRate = 0.95,
            SmallGridCooldownRate = 0.85,
            ShutdownPowerOverMaxSignal = true,
            MaxSignalforPowerShutdown = 500000,
            ShutdownThrustersOverMaxSignal = true,
            MaxSignalforThrusterShutdown = 500000


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
        public double LargeGridCooldownRate { get; set; }

        [ProtoMember(6)]
        public double SmallGridCooldownRate { get; set; }

        [ProtoMember(7)]
        public bool ShutdownPowerOverMaxSignal { get; set; }

        [ProtoMember(8)]
        public double MaxSignalforPowerShutdown { get; set; }

        [ProtoMember(9)]
        public bool ShutdownThrustersOverMaxSignal { get; set; }

        [ProtoMember(10)]
        public double MaxSignalforThrusterShutdown { get; set; }




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
                TextReader reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(ServerSettings));
                string text = reader.ReadToEnd();
                reader.Close();
                s = MyAPIGateway.Utilities.SerializeFromXML<ServerSettings>(text);
                ServerSettings.Instance = s;
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
        }
    }
}

