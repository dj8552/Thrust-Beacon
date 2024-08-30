using ProtoBuf;
using VRageMath;

namespace ThrustBeacon
{
    [ProtoContract]
    public class SignalComp //This is passed from the server to each client per grid in range
    {
        [ProtoMember(1)]
        public Vector3I position; //Using a vector 3I to save on data- ints being smaller than floats or doubles
        [ProtoMember(2)]
        public int range;
        [ProtoMember(3)]
        public string faction;
        [ProtoMember(4)]
        public long entityID;
        [ProtoMember(5)]
        public byte sizeEnum;
        [ProtoMember(6)]
        public byte relation; //1 = enemy, 0 = neutral, 3 = friendly, 4 = own
        [ProtoMember(7)]
        public byte quantity = 1;
    }
}
