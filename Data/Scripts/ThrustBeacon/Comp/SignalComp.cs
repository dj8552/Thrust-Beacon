using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;

namespace ThrustBeacon
{
    [ProtoContract]
    public class SignalComp
    {
        [ProtoMember(1)]
        public Vector3D position;
        [ProtoMember(2)]
        public int range;
        [ProtoMember(3)]
        public string message;
        [ProtoMember(4)]
        public long entityID;
    }
}
