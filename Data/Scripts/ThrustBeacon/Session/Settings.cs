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
    public class Settings
    {
        public static Settings Instance;
        public static readonly Settings Default = new Settings()
        {
            signalColor = Color.Yellow,
            symbolWidth = 0.04f,
            offscreenWidth = 0.1f,
            fadeOutTime = 90,
            maxContactAge = 500,
            textSize = 1f,
            textSizeOwn = 1f,
            signalDrawCoords = new Vector2D(-0.7, -0.625)
        };

        [ProtoMember(1)]
        public Color signalColor { get; set; }

        [ProtoMember(2)]
        public float symbolWidth { get; set; }

        [ProtoMember(3)]
        public float offscreenWidth { get; set; }

        [ProtoMember(4)]
        public int fadeOutTime { get; set; }

        [ProtoMember(5)]
        public int maxContactAge { get; set; }

        [ProtoMember(6)]
        public float textSize { get; set; }

        [ProtoMember(7)]
        public float textSizeOwn { get; set; }

        [ProtoMember(8)]
        public Vector2D signalDrawCoords { get; set; }
    }
    public partial class Session
    {
        private void InitConfig()
        {
            Settings s = Settings.Default;
            var Filename = "Config.cfg";
            var localFileExists = MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(Settings));
            if(localFileExists)
            {
                TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(Filename, typeof(Settings));
                string text = reader.ReadToEnd();
                reader.Close();
                s = MyAPIGateway.Utilities.SerializeFromXML<Settings>(text);
                Settings.Instance = s;
            }
            else
            {
                s = Settings.Default;
                Save(s);
            }
        }
        public void Save(Settings settings)
        {
            var Filename = "Config.cfg";
            TextWriter writer;
            writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(Filename, typeof(Settings));
            writer.Write(MyAPIGateway.Utilities.SerializeToXML(settings));
            writer.Close();
            Settings.Instance = settings;
        }

        private void InitMenu()
        {
            //menu stuff here

        }

    }
}

