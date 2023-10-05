using ProtoBuf;
using VRageMath;

namespace ThrustBeacon
{
    [ProtoContract]
    public class SignalComp
    {
        [ProtoMember(1)]
        public Vector3I position;
        [ProtoMember(2)]
        public int range;
        [ProtoMember(3)]
        public string faction;
        [ProtoMember(4)]
        public long entityID;
        [ProtoMember(5)]
        public byte sizeEnum;
        [ProtoMember(6)]
        public byte relation; //1 = enemy, 0 = neutral, 3 = friendly
        [ProtoMember(7)]
        public int accuracy;
    }
}
