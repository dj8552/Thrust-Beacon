using ProtoBuf;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.IO;
using VRage.Utils;
using VRage.Serialization;
using System.ComponentModel;

namespace ThrustBeacon
{

    public class BlockConfigDict
    {
        [ProtoMember(1)]
        public SerializableDictionary<string, BlockConfig> cfg { get; set; }
    }

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
            var Filename = "BlockConfig.cfg";
            var localFileExists = MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(BlockConfigDict));
            if (localFileExists)
            {
                TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(Filename, typeof(BlockConfigDict));
                var configListTemp = MyAPIGateway.Utilities.SerializeFromXML<List<BlockConfig>>(reader.ReadToEnd()); 
                reader.Close();
                foreach (var temp in configListTemp)
                    BlockConfigs.Add(MyStringHash.GetOrCompute(temp.subTypeID), temp);
                MyLog.Default.WriteLineAndConsole(ModName + $"Loaded {BlockConfigs.Count} blocks from block config");
            }
            else
            {
                WriteBlockDefaults();
            }
        }
        public void WriteBlockDefaults()
        {
            var Filename = "BlockConfig.cfg";
            var tempCfg = new BlockConfigDict();
            tempCfg.cfg = new SerializableDictionary<string, BlockConfig>();

            var sample1 = new BlockConfig(); 
            //This would emulate a passive heat sink, which improves the cooldown rate but does add a
            //fixed 1km to signal as the thermal emission/reflective surface would be more easily detected
            sample1.subTypeID = "heatsinkExample";
            sample1.SignalRange = 1000;
            sample1.SignalCooldown = -0.02f;

            var sample2 = new BlockConfig();
            //This would emulate a passive antenna that adds 5KM to the range to detect other grids,
            //but the risk of bouncing signals back increases the range to detect this grid by 2.5KM
            sample2.subTypeID = "detectionAntenna";
            sample2.SignalRange = 2500;
            sample2.DetectionRange = 5000;

            var sample3 = new BlockConfig();
            //This would emulate an actively powered internal heatsink (aka Mass Effect Normandy), decreasing detection range by 10km
            //but at the cost of slower cooldown, degraded range of your sensors by 2.5km, and high energy cost to have active (energy via SBC)
            sample3.subTypeID = "stealthDrive";
            sample3.SignalRange = -10000;
            sample3.SignalCooldown = 0.01f;
            sample3.DetectionRange = -2500;


            tempCfg.cfg.Dictionary.Add(sample1.subTypeID, sample1);
            tempCfg.cfg.Dictionary.Add(sample2.subTypeID, sample2);
            tempCfg.cfg.Dictionary.Add(sample3.subTypeID, sample3);

            TextWriter writer;
            writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(BlockConfigDict));
            writer.Write(MyAPIGateway.Utilities.SerializeToXML(tempCfg));
            writer.Close();
            MyLog.Default.WriteLineAndConsole(ModName + "Wrote sample block config");
        }
    }

    public class SignalProducerCfgDict
    {
        [ProtoMember(1)]
        public SerializableDictionary<string, int> cfg { get; set; }
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
            var localFileExists = MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(ProducerConfig));
            if (localFileExists)
            {
                TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(Filename, typeof(ProducerConfig));
                var configListTemp = MyAPIGateway.Utilities.SerializeFromXML<List<ProducerConfig>>(reader.ReadToEnd());
                reader.Close();
                foreach (var temp in configListTemp)
                    SignalProducer.Add(temp.subTypeID, temp.divisor);
                MyLog.Default.WriteLineAndConsole(ModName + $"loaded {SignalProducer.Count} signal producers from config");
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
            var tempCfg = new SignalProducerCfgDict();
            tempCfg.cfg = new SerializableDictionary<string, int>();
            foreach (var temp in sampleMap)
            {
                tempCfg.cfg.Dictionary.Add(temp.Key, temp.Value);
                SignalProducer.Add(temp.Key, temp.Value);
            }

            TextWriter writer;
            writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(SignalProducerCfgDict));
            writer.Write(MyAPIGateway.Utilities.SerializeToXML(tempCfg));
            writer.Close();
            MyLog.Default.WriteLineAndConsole(ModName + "Wrote default signal producer config");

        }
    }   
}

