using ProtoBuf;
using System.Collections.Generic;
using ThrustBeacon;
using VRage;

namespace Digi.Example_NetworkProtobuf
{
    [ProtoContract]
    public class PacketBase
    {

        [ProtoMember(1)]
        public List<SignalComp> signalData;

        public PacketBase() { } // Empty constructor required for deserialization

        public PacketBase(List<SignalComp> signaldata)
        {
            signalData = signaldata;
        }
        public bool Received()
        {
            foreach (var signalRcvd in signalData)
            {
                if(Session.SignalList.ContainsKey(signalRcvd.entityID)) //Client already had this in their list, update data
                {
                    var updateTuple = new MyTuple<SignalComp, int>(signalRcvd, Session.Tick);
                    Session.SignalList[signalRcvd.entityID] = updateTuple;
                }
                else //This contact is new to the client
                {
                    Session.SignalList.TryAdd(signalRcvd.entityID, new MyTuple<SignalComp, int>(signalRcvd, Session.Tick));
                }
            }
            return false;           
        }
    }
}
