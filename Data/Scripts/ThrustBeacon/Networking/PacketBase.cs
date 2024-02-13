using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using ThrustBeacon;
using VRage;
using VRage.Game.Entity;
using VRage.Utils;

namespace Digi.Example_NetworkProtobuf
{
    [ProtoInclude(1000, typeof(PacketSettings))]
    [ProtoInclude(2000, typeof(PacketSignals))]
    [ProtoContract]
    public abstract class PacketBase
    {
        //[ProtoMember(1)]
        //public ulong SenderId;
        public PacketBase() { } // Empty constructor required for deserialization
        //public PacketBase()
        //{
        //    SenderId = MyAPIGateway.Multiplayer.MyId;
        //}
        public abstract bool Received();
    }

    [ProtoContract]
    public partial class PacketSignals : PacketBase
    {
        [ProtoMember(100)]
        public List<SignalComp> signalData;
        public PacketSignals() { } // Empty constructor required for deserialization
        public PacketSignals(List<SignalComp> signaldata)
        {
            signalData = signaldata;
        }
        public override bool Received()
        {
            //Clientside list processing to deconflict items shown by WC Radar
            if (Settings.Instance.hideWC)
            {
                var threatList = new List<MyTuple<MyEntity, float>>();
                var obsList = new List<MyEntity>();
                Session.entityIDList.Clear();
                var controlledEnt = MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity?.Parent;
                if (controlledEnt != null && controlledEnt is MyCubeGrid)
                {
                    var myEnt = (MyEntity)controlledEnt;
                    Session.wcAPI.GetSortedThreats(myEnt, threatList);
                    foreach (var item in threatList)
                        Session.entityIDList.Add(item.Item1.EntityId);
                    Session.wcAPI.GetObstructions(myEnt, obsList);
                    foreach (var item in obsList)
                        Session.entityIDList.Add(item.EntityId);
                }
            }

            //Clientside addition or update of signal data
            foreach (var signalRcvd in signalData)
            {
                bool suppressWC = Settings.Instance.hideWC && Session.entityIDList.Contains(signalRcvd.entityID);

                if (Session.SignalList.ContainsKey(signalRcvd.entityID)) //Client already had this in their list, update data
                {
                    if (suppressWC)
                    {
                        Session.SignalList.Remove(signalRcvd.entityID);
                        continue;
                    }
                    Session.SignalList[signalRcvd.entityID] = new MyTuple<SignalComp, int>(signalRcvd, Session.Tick);
                }
                else //This contact is new to the client
                {
                    if (suppressWC)
                        continue;
                    Session.SignalList.TryAdd(signalRcvd.entityID, new MyTuple<SignalComp, int>(signalRcvd, Session.Tick));
                }
            }

            return false;
        }
    }

    [ProtoContract]
    public partial class PacketSettings : PacketBase
    {
        [ProtoMember(200)]
        public List<string> Labels;
        [ProtoMember(201)]
        public bool ClientBeacon;
        public PacketSettings() { } // Empty constructor required for deserialization
        public PacketSettings(List<string> labels, bool clientBeacon)
        {
            Labels = labels;
            ClientBeacon = clientBeacon;
        }
        public override bool Received()
        {
            Session.messageList = Labels;
            Session.clientUpdateBeacon = ClientBeacon;
            MyLog.Default.WriteLineAndConsole($"{Session.ModName}: Received server label list");
            return false;
        }
    }
}
