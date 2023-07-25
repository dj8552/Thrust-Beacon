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
            friendColor = Color.Green,
            enemyColor = Color.Red,
            neutralColor = Color.White,
            symbolWidth = 0.04f,
            offscreenWidth = 0.1f,
            fadeOutTime = 90,
            maxContactAge = 500,
            textSize = 1f,
            textSizeOwn = 1f,
            signalDrawCoords = new Vector2D(-0.7, -0.625)
        };

        [ProtoMember(1)]
        public Color friendColor { get; set; }

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

        [ProtoMember(9)]
        public Color enemyColor { get; set; }

        [ProtoMember(10)]
        public Color neutralColor { get; set; }
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
        HudAPIv2.MenuRootCategory SettingsMenu;
        HudAPIv2.MenuSubCategory MoveSignalDisplay;
        HudAPIv2.MenuItem MoveLeft, MoveRight, MoveUp, MoveDown, MoveReset, Reset, Blank;
        HudAPIv2.MenuColorPickerInput FriendColor, EnemyColor, NeutralColor;
        HudAPIv2.MenuTextInput TextSize, OwnTextSize, SymbolSize;


        private void InitMenu()
        {
            SettingsMenu = new HudAPIv2.MenuRootCategory("Thrust Signal", HudAPIv2.MenuRootCategory.MenuFlag.PlayerMenu, "Thrust Signal Settings");
            FriendColor = new HudAPIv2.MenuColorPickerInput("Select Friendly Signal Color >>", SettingsMenu, Settings.Instance.friendColor, "Select Color", ColorFriend);
            EnemyColor = new HudAPIv2.MenuColorPickerInput("Select Enemy Signal Color >>", SettingsMenu, Settings.Instance.enemyColor, "Select Color", ColorEnemy);
            NeutralColor = new HudAPIv2.MenuColorPickerInput("Select Neutral Signal Color >>", SettingsMenu, Settings.Instance.neutralColor, "Select Color", ColorNeutral);


            SymbolSize = new HudAPIv2.MenuTextInput("Adjust Symbol Size >>", SettingsMenu, "Adjust Symbol Size - Default is 0.04", AdjSymbolSize);
            TextSize = new HudAPIv2.MenuTextInput("Adjust Label Text Size >>", SettingsMenu, "Adjust Label Text Size - Default is 1", AdjLabelSize);
            OwnTextSize = new HudAPIv2.MenuTextInput("Adjust Broadcast Info Size >>", SettingsMenu, "Adjust Broadcast Info Size - Default is 1", AdjLabelSize);
            MoveSignalDisplay = new HudAPIv2.MenuSubCategory("Move Broadcast Info Location >>", SettingsMenu, "Broadcast Info Location");
                MoveLeft = new HudAPIv2.MenuItem("Move Left", MoveSignalDisplay, LeftMove);
                MoveRight = new HudAPIv2.MenuItem("Move Right", MoveSignalDisplay, RightMove);
                MoveUp = new HudAPIv2.MenuItem("Move Up", MoveSignalDisplay, UpMove);
                MoveDown = new HudAPIv2.MenuItem("Move Down", MoveSignalDisplay, DownMove);
                MoveReset = new HudAPIv2.MenuItem("Reset Position", MoveSignalDisplay, ResetMove);
            Blank = new HudAPIv2.MenuItem("- - - - - - - - - - -", SettingsMenu, null);

            Reset = new HudAPIv2.MenuItem("Reset all Settings", SettingsMenu, ResetSettings);
        }

        private void LeftMove()
        {
            Settings.Instance.signalDrawCoords += new Vector2D(-0.01, 0);
        }
        private void RightMove()
        {
            Settings.Instance.signalDrawCoords += new Vector2D(0.01, 0);
        }
        private void UpMove()
        {
            Settings.Instance.signalDrawCoords += new Vector2D(0, 0.01);
        }
        private void DownMove()
        {
            Settings.Instance.signalDrawCoords += new Vector2D(0, -0.01);
        }
        private void ResetMove()
        {
            Settings.Instance.signalDrawCoords = new Vector2D(-0.7, -0.625);
        }
        private void ColorFriend(Color obj)
        {
            Settings.Instance.friendColor = obj;
        }
        private void ColorNeutral(Color obj)
        {
            Settings.Instance.neutralColor = obj;
        }
        private void ColorEnemy(Color obj)
        {
            Settings.Instance.enemyColor = obj;
        }

        private void AdjSymbolSize(string obj)
        {
            float getter;
            if (!float.TryParse(obj, out getter))
                return;
            Settings.Instance.symbolWidth = getter;
        }

        private void AdjLabelSize(string obj)
        {
            float getter;
            if (!float.TryParse(obj, out getter))
                return;
            Settings.Instance.textSize = getter;
        }
        private void AdjOwnLabelSize(string obj)
        {
            float getter;
            if (!float.TryParse(obj, out getter))
                return;
            Settings.Instance.textSizeOwn = getter;
        }
        private void ResetSettings()
        {
            MyAPIGateway.Utilities.ShowNotification("Options reset to default");
            var tempSettings = new Settings();
            tempSettings = Settings.Default;
            Settings.Instance = tempSettings;
            Save(tempSettings);
        }

    }
}

