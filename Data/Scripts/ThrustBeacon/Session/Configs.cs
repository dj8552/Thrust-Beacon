using ProtoBuf;
using Draygo.API;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using VRage.Utils;
using VRageMath;
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
    public class BlockConfig //TODO flesh out all the options for blocks to modify signals (accuracy, detection range boost, stealth, faster cooldown, etc)
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

        [ProtoMember(5)]
        [DefaultValue(0)]
        public float DetectionAccuracy { get; set; }

    }
    public partial class Session
    {
        private void LoadBlockConfigs()
        {
            var Filename = "BlockConfig.cfg";
            var localFileExists = MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(BlockConfigDict));
            if (localFileExists)
            {
                var configListTemp = new List<BlockConfig>();
                TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(Filename, typeof(BlockConfigDict));
                configListTemp = MyAPIGateway.Utilities.SerializeFromXML<List<BlockConfig>>(reader.ReadToEnd()); 
                reader.Close();
                foreach (var temp in configListTemp)
                    BlockConfigs.Add(MyStringHash.GetOrCompute(temp.subTypeID), temp);
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
            sample1.subTypeID = "test";
            sample1.SignalRange = 100;
            sample1.DetectionRange = 200;
            sample1.DetectionAccuracy = 1;
            sample1.SignalCooldown = 1;

            tempCfg.cfg.Dictionary.Add(sample1.subTypeID, sample1);
            BlockConfigs.Add(MyStringHash.GetOrCompute(sample1.subTypeID), sample1);

            TextWriter writer;
            writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(BlockConfigDict));
            writer.Write(MyAPIGateway.Utilities.SerializeToXML(tempCfg));
            writer.Close();
        }
    }

    public class ThrusterConfigDict
    {
        [ProtoMember(1)]
        public SerializableDictionary<string, int> cfg { get; set; }
    }

    [ProtoContract]
    public class ThrusterConfig
    {
        [ProtoMember(1)]
        public string subTypeID { get; set; }

        [ProtoMember(2)]
        public int divisor { get; set; }
    }
    public partial class Session
    {
        private void LoadSignalProducerConfigs()
        {
            var Filename = "SignalProducerConfig.cfg";
            var localFileExists = MyAPIGateway.Utilities.FileExistsInLocalStorage(Filename, typeof(ThrusterConfig));
            if (localFileExists)
            {
                var configListTemp = new List<ThrusterConfig>();
                TextReader reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(Filename, typeof(ThrusterConfig));
                string text = reader.ReadToEnd();
                reader.Close();
                foreach (var temp in configListTemp)
                    SignalProducer.Add(temp.subTypeID, temp.divisor);
            }
            else
            {
                WriteThrusterDefaults();
            }
        }
        public void WriteThrusterDefaults()
        {
            var Filename = "ThrusterConfig.cfg";
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
            var tempCfg = new ThrusterConfigDict();
            tempCfg.cfg = new SerializableDictionary<string, int>();
            foreach (var temp in sampleMap)
            {
                tempCfg.cfg.Dictionary.Add(temp.Key, temp.Value);
                SignalProducer.Add(temp.Key, temp.Value);
            }

            TextWriter writer;
            writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(Filename, typeof(ThrusterConfigDict));
            writer.Write(MyAPIGateway.Utilities.SerializeToXML(tempCfg));
            writer.Close();
        }
    }   
}

