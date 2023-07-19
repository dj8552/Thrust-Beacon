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
        internal long broadcastDistSqr;
        internal string faction = "";
        internal VRage.Game.MyCubeSize gridSize;
        internal byte sizeEnum;

        internal Dictionary<string, int> epsteinDivisorMap = new Dictionary<string, int>()
        {
            { "arylnx_raider_epstein_drive", 733 },
            { "arylnx_quadra_epstein_drive", 625 },
            { "arylnx_munr_epstein_drive", 1385 },
            { "arylnx_epstein_drive", 1000 },
            { "arylnx_roci_epstein_drive", 1138},
            { "arylynx_silversmith_epstein_drive", 750 },
            { "arylnx_scircocco_epstein_drive", 1447 },
            { "arylnx_mega_epstein_drive", 1440 },
            { "arylnx_rzb_epstein_drive", 250 },
            { "aryxlnx_yacht_epstein_drive", 1250 },
            { "arylnx_pndr_epstein_drive", 1052 },
            { "arylnx_drummer_epstein_drive", 1206 },
            { "arylnx_leo_epstein_drive", 1233 },
        };

        internal void Init(MyCubeGrid grid, Session session)
        {
            Grid = grid;
            Grid.OnFatBlockAdded += FatBlockAdded;
            Grid.OnFatBlockRemoved += FatBlockRemoved;
            gridSize = Grid.GridSizeEnum;
        }

        internal void FatBlockAdded(MyCubeBlock block)
        {
            if(block.CubeGrid.BigOwners != null && block.CubeGrid.BigOwners.Count > 0)
            {
                var curFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(block.CubeGrid.BigOwners[0]);
                if (curFaction != null)
                {
                    faction = curFaction.Tag + ".";
                }
            }

            var power = block as IMyPowerProducer;
            if (power != null)
                powerList.Add(power);

            var thruster = block as IMyThrust;
            if (thruster != null)
            {
                var name = thruster.BlockDefinition.SubtypeId.ToLower();
                int divisor;
                if (name.Contains("rcs"))
                    divisor = 5184;
                else if (name.Contains("mesx"))
                    divisor = 5184;
                else
                {
                    if (!epsteinDivisorMap.TryGetValue(name, out divisor))
                    {
                        divisor = 600; // Default if unknown thruster
                    }
                }
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
            if (thrustList.Count == 0 && !powerShutdown)
            {
                broadcastDist = 0;
                sizeEnum = 0;
                return;
            }

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

            if (broadcastDist < 100f)//Idle
            {
                sizeEnum = 0;
            }
            else if (broadcastDist < 8000f)//Small
            {
                sizeEnum = 1;
            }
            else if (broadcastDist < 25000f)//Medium
            {
                sizeEnum = 2;
            }
            else if (broadcastDist < 100000f)//Large
            {
                sizeEnum = 3;
            }
            else if (broadcastDist < 250000f)//Huge
            {
                sizeEnum = 4;
            }
            else//Massive
            {
                sizeEnum = 5;
            }

            if (broadcastDist >= 500000 && !powerShutdown)
                Session.shutdownList.Add(this);
            else if (broadcastDist < 500000 && powerShutdown)
                Session.shutdownList.Remove(this);
            broadcastDistSqr = (long)broadcastDist * broadcastDist;
            return;
        }

        internal void TogglePower()
        {
            foreach (var power in powerList.ToArray())
                if (power.Enabled && !power.MarkedForClose)
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
