using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace ThrustBeacon
{
    internal class GridComp
    {
        internal MyCubeGrid Grid;
        internal Dictionary<IMyThrust, int> thrustList = new Dictionary<IMyThrust, int>();
        internal List<IMyPowerProducer> powerList = new List<IMyPowerProducer>();
        internal bool powerShutdown = false;
        internal int broadcastDist;
        internal int broadcastDistOld;
        internal int broadcastDistSqr;
        internal string broadcastMsg;
        internal VRage.Game.MyCubeSize gridSize;

        internal void Init(MyCubeGrid grid, Session session)
        {
            Grid = grid;
            Grid.OnFatBlockAdded += FatBlockAdded;
            Grid.OnFatBlockRemoved += FatBlockRemoved;
            gridSize = Grid.GridSizeEnum;
        }

        internal void FatBlockAdded(MyCubeBlock block)
        {
            var power = block as IMyPowerProducer;
            if (power != null)
                powerList.Add(power);

            var thruster = block as IMyThrust;
            if (thruster != null)
            {
                var name = thruster.BlockDefinition.SubtypeId.ToLower();
                int divisor;
                if (name == "arylnx_raider_epstein_drive")
                    divisor = 733;

                else if (name == "arylnx_quadra_epstein_drive")
                    divisor = 625;

                else if (name == "arylnx_munr_epstein_drive")
                    divisor = 1385;

                else if (name == "arylnx_epstein_drive")
                    divisor = 1000;

                else if (name == "arylnx_roci_epstein_drive")
                    divisor = 1138;

                else if (name == "arylynx_silversmith_epstein_drive")
                    divisor = 1750;

                else if (name == "arylnx_scircocco_epstein_drive")
                    divisor = 1447;

                else if (name == "arylnx_mega_epstein_drive")
                    divisor = 1440;

                else if (name == "arylnx_rzb_epstein_drive")
                    divisor = 1250;

                else if (name == "aryxlnx_yacht_epstein_drive")
                    divisor = 1250;

                else if (name == "arylnx_pndr_epstein_drive")
                    divisor = 1052;

                else if (name == "arylnx_drummer_epstein_drive")
                    divisor = 1206;

                else if (name == "arylnx_leo_epstein_drive")
                    divisor = 1233;

                else if (name.Contains("rcs"))
                    divisor = 5184;

                else if (name.Contains("mesx"))
                    divisor = 5184;

                else divisor = 600;
                thrustList.Add(thruster, divisor);
            }
        }


        internal void FatBlockRemoved(MyCubeBlock block)
        {
            var thrust = block as IMyThrust;
            if (thrust != null)
                thrustList.Remove(thrust);
            var power = block as IMyPowerProducer;
            if (power != null)
                powerList.Remove(power);
        }

        internal void CalcThrust()
        {
            broadcastDistOld = broadcastDist;

            if (!Grid.IsStatic)
            {
                double rawThrustOutput = 0.0d;
                foreach (var thrust in thrustList)
                {
                    if (thrust.Key.CurrentThrust == 0)
                        continue;
                    rawThrustOutput += thrust.Key.CurrentThrust / thrust.Value;
                }
                broadcastDist = (int)rawThrustOutput;
            }
            if (broadcastDistOld > broadcastDist || Grid.IsStatic)
            {
                if (gridSize == 0)
                    broadcastDist = (int)(broadcastDistOld * 0.95f);
                else
                    broadcastDist = (int)(broadcastDistOld * 0.85f);
                if (broadcastDist <= 1)
                    broadcastDist = 1;
            }

            if (broadcastDist < 100f)
            {
                broadcastMsg = "Idle Drive Signature";
            }
            else if (broadcastDist >= 100f && broadcastDist < 8000f)
            {
                broadcastMsg = "Small Drive Signature";
            }
            else if (broadcastDist >= 8001f && broadcastDist < 25000f)
            {
                broadcastMsg = "Medium Drive Signature";
            }
            else if (broadcastDist >= 25001f && broadcastDist < 100000f)
            {
                broadcastMsg = "Large Drive Signature";
            }
            else if (broadcastDist >= 100001f && broadcastDist < 250000f)
            {
                broadcastMsg = "Huge Drive Signature";
            }
            else if (broadcastDist >= 250001f && broadcastDist < 500000f)
            {
                broadcastMsg = "Massive Drive Signature";
            }
            else if (broadcastDist >= 500001f)
            {
                broadcastMsg = "Immense Drive Signature";
            }

            if (broadcastDist >= 500000 && !powerShutdown)
                Session.shutdownList.Add(this);
            else if (broadcastDist < 500000 && powerShutdown)
                Session.shutdownList.Remove(this);
            broadcastDistSqr = broadcastDist * broadcastDist;
            return;
        }

        internal void TogglePower()
        {
            foreach (var power in powerList)
                if (!power.MarkedForClose && power.Enabled)
                    power.Enabled = false;
        }

        internal void Clean()
        {
            Grid.OnFatBlockAdded -= FatBlockAdded;
            Grid.OnFatBlockRemoved -= FatBlockRemoved;
            Grid = null;
            thrustList.Clear();
            powerList.Clear();
            broadcastDist = 0;
            broadcastDistSqr = 0;
        }
    }
}
