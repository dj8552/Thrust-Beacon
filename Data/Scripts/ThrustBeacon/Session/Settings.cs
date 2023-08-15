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
            offscreenWidth = 0.06f,
            fadeOutTime = 1.5d,
            stopDisplayTime = 8d,
            keepTime = 10d,            
            textSize = 1f,
            textSizeOwn = 1f,
            signalDrawCoords = new Vector2D(-0.7, -0.625),
            newTime = 2d,
        };

        [ProtoMember(1)]
        public Color friendColor { get; set; }

        [ProtoMember(2)]
        public float symbolWidth { get; set; }

        [ProtoMember(3)]
        public float offscreenWidth { get; set; }

        [ProtoMember(4)]
        public double fadeOutTime { get; set; }

        [ProtoMember(5)]
        public double stopDisplayTime { get; set; }

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

        [ProtoMember(11)]
        public double keepTime { get; set; }

        [ProtoMember(12)]
        public double newTime { get; set; }


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
            fadeTimeTicks = (int)(s.fadeOutTime * 60);
            stopDisplayTimeTicks = (int)(s.stopDisplayTime * 60);
            keepTimeTicks = (int)(s.keepTime * 3600);
            newTimeTicks = (int)(s.newTime * 60);

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
        HudAPIv2.MenuSubCategory MoveSignalDisplay, OwnTextSize;
        HudAPIv2.MenuItem MoveLeft, MoveRight, MoveUp, MoveDown, MoveReset, Reset, Blank, IncreaseOwn, DecreaseOwn;
        HudAPIv2.MenuColorPickerInput FriendColor, EnemyColor, NeutralColor;
        HudAPIv2.MenuTextInput TextSize, SymbolSize, NewTime, FadeTime, StopDisplayTime, KeepTime;


        private void InitMenu()
        {
            SettingsMenu = new HudAPIv2.MenuRootCategory("Thrust Signal", HudAPIv2.MenuRootCategory.MenuFlag.PlayerMenu, "Thrust Signal Settings");
            FriendColor = new HudAPIv2.MenuColorPickerInput("Select Friendly Signal Color >>", SettingsMenu, Settings.Instance.friendColor, "Select Color", ColorFriend);
            EnemyColor = new HudAPIv2.MenuColorPickerInput("Select Enemy Signal Color >>", SettingsMenu, Settings.Instance.enemyColor, "Select Color", ColorEnemy);
            NeutralColor = new HudAPIv2.MenuColorPickerInput("Select Neutral Signal Color >>", SettingsMenu, Settings.Instance.neutralColor, "Select Color", ColorNeutral);


            SymbolSize = new HudAPIv2.MenuTextInput("Adjust Symbol Size >>", SettingsMenu, "Adjust Symbol Size - Default is 0.04", AdjSymbolSize);
            TextSize = new HudAPIv2.MenuTextInput("Adjust Label Text Size >>", SettingsMenu, "Adjust Label Text Size - Default is 1", AdjLabelSize);

            OwnTextSize = new HudAPIv2.MenuSubCategory("Adjust Broadcast Info Size >>", SettingsMenu, "Adjust Broadcast Info Size");
                IncreaseOwn = new HudAPIv2.MenuItem("Increase text size", OwnTextSize, IncreaseOwnSz);
                DecreaseOwn = new HudAPIv2.MenuItem("Decrease text size", OwnTextSize, DecreaseOwnSz);


            MoveSignalDisplay = new HudAPIv2.MenuSubCategory("Move Broadcast Info Location >>", SettingsMenu, "Broadcast Info Location");
                MoveLeft = new HudAPIv2.MenuItem("Move Left", MoveSignalDisplay, LeftMove);
                MoveRight = new HudAPIv2.MenuItem("Move Right", MoveSignalDisplay, RightMove);
                MoveUp = new HudAPIv2.MenuItem("Move Up", MoveSignalDisplay, UpMove);
                MoveDown = new HudAPIv2.MenuItem("Move Down", MoveSignalDisplay, DownMove);
                MoveReset = new HudAPIv2.MenuItem("Reset Position", MoveSignalDisplay, ResetMove);
            Blank = new HudAPIv2.MenuItem("- - - - - - - - - - -", SettingsMenu, null);
            NewTime = new HudAPIv2.MenuTextInput("Show new signal alert for " + Settings.Instance.newTime + " seconds", SettingsMenu, "Time to display new signal alert (in seconds)", NewTimeAdj);

            FadeTime = new HudAPIv2.MenuTextInput("Fade out after " + Settings.Instance.fadeOutTime + " seconds", SettingsMenu, "Time to wait before fading out contact (in seconds)", FadeTimeAdj);
            StopDisplayTime = new HudAPIv2.MenuTextInput("Stop displaying after " + Settings.Instance.stopDisplayTime + " seconds", SettingsMenu, "Stop drawing contacts that have not updated (in seconds)", StopTimeAdj);
            KeepTime = new HudAPIv2.MenuTextInput("Purge signal record after " + Settings.Instance.keepTime + " minutes", SettingsMenu, "Purge signal record without updates (in minutes)", KeepTimeAdj);


            Blank = new HudAPIv2.MenuItem("- - - - - - - - - - -", SettingsMenu, null);

            Reset = new HudAPIv2.MenuItem("Reset all Settings", SettingsMenu, ResetSettings);
        }

        private void IncreaseOwnSz()
        {
            Settings.Instance.textSizeOwn += 0.025f;

        }
        private void DecreaseOwnSz()
        {
            Settings.Instance.textSizeOwn -= 0.025f;
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
            FriendColor.InitialColor = obj;
        }
        private void ColorNeutral(Color obj)
        {
            Settings.Instance.neutralColor = obj;
            NeutralColor.InitialColor = obj;
        }
        private void ColorEnemy(Color obj)
        {
            Settings.Instance.enemyColor = obj;
            EnemyColor.InitialColor = obj;
        }

        private void AdjSymbolSize(string obj)
        {
            float getter;
            if (!float.TryParse(obj, out getter))
                return;
            Settings.Instance.symbolWidth = getter;
            symbolHeight = Settings.Instance.symbolWidth * aspectRatio;
            Settings.Instance.offscreenWidth = getter + 0.02f;
            offscreenHeight = Settings.Instance.offscreenWidth * aspectRatio;
        }
        private void AdjLabelSize(string obj)
        {
            float getter;
            if (!float.TryParse(obj, out getter))
                return;
            Settings.Instance.textSize = getter;
        }
        private void NewTimeAdj(string obj)
        {
            double getter;
            if (!double.TryParse(obj, out getter))
                return;
            Settings.Instance.newTime = getter;
            LabelUpdate();
            newTimeTicks = (int)(getter * 60);
        }
        private void FadeTimeAdj(string obj)
        {
            double getter;
            if (!double.TryParse(obj, out getter))
                return;
            Settings.Instance.fadeOutTime = getter;
            LabelUpdate();
            fadeTimeTicks = (int)(getter * 60);
        }
        private void StopTimeAdj(string obj)
        {
            double getter;
            if (!double.TryParse(obj, out getter))
                return;
            Settings.Instance.fadeOutTime = getter;
            LabelUpdate();
            stopDisplayTimeTicks = (int)(getter * 60);
        }
        private void KeepTimeAdj(string obj)
        {
            double getter;
            if (!double.TryParse(obj, out getter))
                return;
            Settings.Instance.keepTime = getter;
            LabelUpdate();
            keepTimeTicks = (int)(getter * 3600);
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
            Settings.Instance = Settings.Default;
            Save(Settings.Instance);
            LabelUpdate();
        }

        private void LabelUpdate()
        {
            KeepTime.Text = "Purge signal record after " + Settings.Instance.keepTime + " minutes";
            StopDisplayTime.Text = "Stop displaying after " + Settings.Instance.stopDisplayTime + " seconds";
            FadeTime.Text = "Fade out after " + Settings.Instance.fadeOutTime + " seconds";
            NewTime.Text = "Show new signal alert for " + Settings.Instance.newTime + " seconds";
        }

    }
}

