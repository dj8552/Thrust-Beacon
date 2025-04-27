using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace ThrustBeacon
{
    public class ApiBackend
    {
        private readonly Session _session;
        internal Dictionary<string, Delegate> PBApiMethods;

        internal ApiBackend(Session session)
        {
            _session = session;
        }

        internal void PbInit()
        {
            PBApiMethods = new Dictionary<string, Delegate>
            {
                ["TBRegisterPB"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool>(PBRegister),
                ["TBGetThrustSignalBroadcastRange"] = new Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int>(PBGetThrustSignalBroadcastRange),
            };
            var pb = MyAPIGateway.TerminalControls.CreateProperty<Dictionary<string, Delegate>, IMyTerminalBlock>("ThrustBeaconAPI");
            pb.Getter = b => PBApiMethods;
            MyAPIGateway.TerminalControls.AddControl<IMyProgrammableBlock>(pb);
            _session.PbApiInited = true;
        }

        private bool PBRegister(object pb)
        {
            //Register PB using Thrust Beacon API to receive updates
            var block = pb as IMyTerminalBlock;
            if (block != null)
            {
                if (!_session.PbDict.ContainsKey(block))
                    _session.PbDict.Add(block, Session.Tick);
                return true;
            }
            return false;
        }

        private int PBGetThrustSignalBroadcastRange(object pb)
        {
            var block = pb as IMyTerminalBlock;
            int update;
            if (block != null && _session.PbDict.TryGetValue(block, out update))
            {
                if (update > Session.Tick)
                    return -1;
                GroupComp groupComp;
                if (Session.GroupDict.TryGetValue(block.CubeGrid.GetGridGroup(GridLinkTypeEnum.Mechanical), out groupComp))
                {
                    _session.PbDict[block] = Session.Tick + Session.rand.Next(5, 45);
                    return groupComp.groupBroadcastDist;
                }
            }
            return -2;
        }
    }
}
