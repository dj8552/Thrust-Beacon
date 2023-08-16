using ProtoBuf;
using System.Collections.Generic;
using ThrustBeacon;
using VRage;
using VRageMath;

namespace Digi.Example_NetworkProtobuf
{
    // tag numbers in ProtoInclude collide with numbers from ProtoMember in the same class, therefore they must be unique.
    [ProtoContract]
    public class PacketBase
    {
        // this field's value will be sent if it's not the default value.
        // to define a default value you must use the [DefaultValue(...)] attribute.
        [ProtoMember(1)]
        public List<SignalComp> signalData;

        public PacketBase() { } // Empty constructor required for deserialization

        public PacketBase(List<SignalComp> signaldata)
        {
            signalData = signaldata;
        }

        /// <summary>
        /// Called when this packet is received on this machine.
        /// </summary>
        /// <returns>Return true if you want the packet to be sent to other clients (only works server side)</returns>
        public bool Received()
        {
            foreach (var signalRcvd in signalData)
            {
                if(Session.SignalList.ContainsKey(signalRcvd.entityID))//TODO:  Any error checking needed on received data?
                {
                    var updateTuple = new MyTuple<SignalComp, int>(signalRcvd, Session.Tick);
                    Session.SignalList[signalRcvd.entityID] = updateTuple;
                }
                else
                {
                    Session.SignalList.TryAdd(signalRcvd.entityID, new MyTuple<SignalComp, int>(signalRcvd, Session.Tick));
                    Session.NewSignalList.TryAdd(signalRcvd.entityID, new MyTuple<SignalComp, int>(signalRcvd, Session.Tick));
                }
            }
            return false;           
        }
    }
}
