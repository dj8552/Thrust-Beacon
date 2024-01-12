using ProtoBuf;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.IO;
using VRage.Utils;
using System.ComponentModel;
using System;

namespace ThrustBeacon
{
    [ProtoContract]
    public class BlockConfig
    {
        [ProtoMember(1)]
        public string subTypeID { get; set; }//Block subtypeID.  Can be a passive or functional block.  Functional blocks will be checked if enabled for any bonuses/effects
        [ProtoMember(2)]
        [DefaultValue(0)]
        public float SignalCooldown { get; set; }//Additive to cooldown mult.  For ex -0.05f would make a grid cooldown faster
        [ProtoMember(3)]
        [DefaultValue(0)]
        public float SignalRange { get; set; }//Meters- Positive to make this grid detectable further, negative to decrease distance for being detected.
        [ProtoMember(4)]
        [DefaultValue(0)]
        public float DetectionRange { get; set; }//Meters- Positive to make detection of others possible at higher dist
    }
    public partial class Session
    {
        private void LoadBlockConfigs()
        {
            //Roll subtype IDs of all WC weapons into a hash set
            List<VRage.Game.MyDefinitionId> tempWeaponDefs = new List<VRage.Game.MyDefinitionId>();
            if (wcAPI != null)
                wcAPI.GetAllCoreWeapons(tempWeaponDefs);
            foreach (var def in tempWeaponDefs)
            {
                weaponSubtypeIDs.Add(def.SubtypeId);
                MyLog.Default.WriteLineAndConsole(ModName + $"Registered {weaponSubtypeIDs.Count} weapon block types");
            }


            var Filename = "BlockConfig.cfg";
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(BlockConfig)))
            {
                TextReader reader = null;
                try
                {
                    reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(BlockConfig));
                    var configListTemp = MyAPIGateway.Utilities.SerializeFromXML<List<BlockConfig>>(reader.ReadToEnd());
                    reader.Close();
                    foreach (var temp in configListTemp)
                        BlockConfigs.Add(MyStringHash.GetOrCompute(temp.subTypeID), temp);
                    MyLog.Default.WriteLineAndConsole(ModName + $"Loaded {BlockConfigs.Count} blocks from block config");
                }
                catch (Exception e)
                {
                    if (reader != null) reader.Close();
                    MyLog.Default.WriteLineAndConsole(ModName + "Error reading block configs file, using defaults - " + e.InnerException);
                    WriteBlockSamples();
                }
            }
            else
            {
                WriteBlockSamples();
            }
        }
        public void WriteBlockSamples()
        {
            var Filename = "BlockConfig.cfg";
            var tempCfg = new List<BlockConfig>()
            {
            new BlockConfig() {subTypeID = "heatsinkExample", SignalRange = 1000, SignalCooldown = -0.02f},
            new BlockConfig() {subTypeID = "detectionAntenna", SignalRange = 2500, DetectionRange = 5000},
            new BlockConfig() {subTypeID = "stealthDrive", SignalRange = -10000, SignalCooldown = 0.01f, DetectionRange = -2500}
            };

            TextWriter writer;
            writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(BlockConfig));
            writer.Write(MyAPIGateway.Utilities.SerializeToXML(tempCfg));
            writer.Close();
            MyLog.Default.WriteLineAndConsole(ModName + "Wrote sample block config");
        }
    }

    [ProtoContract]
    public class ProducerConfig
    {
        [ProtoMember(1)]
        public string subTypeID { get; set; } //Plain text subtypeID for signal producing thruster or power generation blocks
        [ProtoMember(2)]
        public int divisor { get; set; } //Amount to divide current output by.  For thrusters it's current thrust/divisor, for power it's current output/divisor
    }
    public partial class Session
    {
        private void LoadSignalProducerConfigs()
        {
            var Filename = "SignalProducerConfig.cfg";
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage(Filename, typeof(ProducerConfig)))
            {
                TextReader reader = null;
                try
                {
                    reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(Filename, typeof(ProducerConfig));
                    var configListTemp = MyAPIGateway.Utilities.SerializeFromXML<List<ProducerConfig>>(reader.ReadToEnd());
                    reader.Close();
                    foreach (var temp in configListTemp)
                        SignalProducer.Add(temp.subTypeID, temp.divisor);
                    MyLog.Default.WriteLineAndConsole(ModName + $"loaded {SignalProducer.Count} signal producers from config");
                }
                catch (Exception e)
                {
                    if (reader != null) reader.Close();
                    MyLog.Default.WriteLineAndConsole(ModName + "Error reading signal producers file, using defaults - " + e.InnerException);
                    WriteProducerDefaults();
                }
            }
            else
            {
                WriteProducerDefaults();
            }
        }
        public void WriteProducerDefaults()
        {
            var Filename = "SignalProducerConfig.cfg";
            var sampleMap = new Dictionary<string, int>()           
            {
                { "ARYLNX_RAIDER_Epstein_Drive", 733 },
                { "ARYLNX_QUADRA_Epstein_Drive", 625 },
                { "ARYLNX_MUNR_Epstein_Drive", 1385 },
                { "ARYLNX_Epstein_Drive", 1000 },
                { "ARYLNX_ROCI_Epstein_Drive", 1138},
                { "ARYLYNX_SILVERSMITH_Epstein_DRIVE", 750 },
                { "ARYLNX_SCIRCOCCO_Epstein_Drive", 1447 },
                { "ARYLNX_Mega_Epstein_Drive", 1440 },
                { "ARYLNX_RZB_Epstein_Drive", 250 },
                { "ARYXLNX_YACHT_EPSTEIN_DRIVE", 1250 },
                { "ARYLNX_PNDR_Epstein_Drive", 1052 },
                { "ARYLNX_DRUMMER_Epstein_Drive", 1206 },
                { "ARYLNX_Leo_Epstein_Drive", 1233 },
                { "LynxRcsThruster1", 5184 },
                { "AryxRCSRamp", 5184 },
                { "AryxRCSHalfRamp", 5184},
                { "AryxRCSSlant", 5184},
                { "AryxRCS", 5184},
                { "ARYLNX_MESX_Epstein_Drive1", 5184},
                { "ARYLNX_MESX_Epstein_Drive2", 5184},
                { "Silverfish_RCS", 5184},
                { "SmallBlockSmallGenerator", 12345}

            };
            var tempCfg = new List<ProducerConfig>();
            foreach (var temp in sampleMap)
            {
                var newCfg = new ProducerConfig() {subTypeID = temp.Key, divisor = temp.Value};
                tempCfg.Add(newCfg);
                SignalProducer.Add(temp.Key, temp.Value);
            }

            TextWriter writer;
            writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(ProducerConfig));
            writer.Write(MyAPIGateway.Utilities.SerializeToXML(tempCfg));
            writer.Close();
            MyLog.Default.WriteLineAndConsole(ModName + "Wrote default signal producer config");

        }
    }   
}

