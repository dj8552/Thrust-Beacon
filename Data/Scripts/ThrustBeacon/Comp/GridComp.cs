using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace ThrustBeacon
{
    internal class GridComp
    {
        private Session _session;
        internal MyCubeGrid Grid;
        internal List<IMyBeacon> beaconList = new List<IMyBeacon>();
        internal Dictionary<IMyThrust, int> thrustList = new Dictionary<IMyThrust, int>();
        internal int broadcastDist;
        internal int broadcastDistOld;
        internal string broadcastMsg;
        internal VRage.Game.MyCubeSize gridSize;

        internal void Init(MyCubeGrid grid, Session session)
        {
            _session = session;
            Grid = grid;
            Grid.OnFatBlockAdded += FatBlockAdded;
            Grid.OnFatBlockRemoved += FatBlockRemoved;
            gridSize = Grid.GridSizeEnum;
        }

        internal void FatBlockAdded(MyCubeBlock block)
        {
            var beacon = block as IMyBeacon;
            var thruster = block as IMyThrust;
            if (beacon != null)
            {
                beaconList.Add(beacon);
                beacon.EnabledChanged += Beacon_EnabledChanged;
                beacon.PropertiesChanged += Beacon_PropertiesChanged;
            }
            if (thruster != null)
            {
                var name = thruster.BlockDefinition.SubtypeId.ToLower();
                int divisor;
                if (name == "arylnx_raider_epstein_drive")
                    divisor =  733;

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

        private void Beacon_EnabledChanged(IMyTerminalBlock obj)
        {
            var beacon = obj as IMyBeacon;
            if (!beacon.Enabled)
                beacon.Enabled = true;
        }

        private void Beacon_PropertiesChanged(IMyTerminalBlock obj)
        {
            var beacon = obj as IMyBeacon;
            if (beacon.HudText != broadcastMsg || beacon.Radius != broadcastDist)
            {
                beacon.Radius = broadcastDist;
                beacon.HudText = broadcastMsg;
            }
        }

        internal void FatBlockRemoved(MyCubeBlock block)
        {
            var beacon = block as IMyBeacon;
            var thrust = block as IMyThrust;
            if (beacon != null)
            {
                beaconList.Remove(beacon);
                beacon.EnabledChanged -= Beacon_EnabledChanged;
                beacon.PropertiesChanged -= Beacon_PropertiesChanged;
            }
            if (thrust != null)
                thrustList.Remove(thrust);
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
            else
            {
                broadcastDist = 1;
            }
            if (broadcastDistOld > broadcastDist)
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
            else if (broadcastDist>= 8001f && broadcastDist < 25000f)
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
            else if (broadcastDist >= 500001f && broadcastDist < 1000000f)
            {
                broadcastMsg = "Immense Drive Signature";
            }
            else if (broadcastDist >= 1000001f)
            {
                broadcastMsg = "Capital Drive Signature";
            }
            return;
        }
    

        internal void Clean()
        {
            Grid.OnFatBlockAdded -= FatBlockAdded;
            Grid.OnFatBlockRemoved -= FatBlockRemoved;
            Grid = null;
            foreach (var beacon in beaconList)
            {
                beacon.EnabledChanged -= Beacon_EnabledChanged;
                beacon.PropertiesChanged -= Beacon_PropertiesChanged;
            }
            beaconList.Clear();
            thrustList.Clear();
            broadcastDist = 0;
        }
    }
}
