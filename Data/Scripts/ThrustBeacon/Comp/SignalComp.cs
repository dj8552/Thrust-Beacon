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
        public int range; //TODO: Look at whether we can have clients calc this (if it's worth saving sending 1 int over the 'net)
        [ProtoMember(3)]
        public string faction;
        [ProtoMember(4)]
        public long entityID;
        [ProtoMember(5)]
        public byte sizeEnum;
    }
}
