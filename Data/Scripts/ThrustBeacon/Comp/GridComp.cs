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
        internal Dictionary<IMyPowerProducer, int> powerList = new Dictionary<IMyPowerProducer, int>();
        internal bool powerShutdown = false;
        internal bool thrustShutdown = false;
        internal int broadcastDist;
        internal int broadcastDistOld;
        internal long broadcastDistSqr;
        internal string faction = "";
        internal long factionID = 0;
        internal VRage.Game.MyCubeSize gridSize;
        internal byte sizeEnum;
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
                    factionID = curFaction.FactionId;
                }
            }

            var power = block as IMyPowerProducer;
            if (power != null)
            {
                int divisor;
                if (!Session.SignalProducer.TryGetValue(power.BlockDefinition.SubtypeId, out divisor))
                {
                    divisor = ServerSettings.Instance.DefaultPowerDivisor;
                }
                powerList.Add(power, divisor);
            }

            var thruster = block as IMyThrust;
            if (thruster != null)
            {
                int divisor;
                if (!Session.SignalProducer.TryGetValue(thruster.BlockDefinition.SubtypeId, out divisor))
                {
                    divisor = ServerSettings.Instance.DefaultThrustDivisor;
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

        internal void CalcSignal()
        {
            if ((thrustList.Count == 0 && !powerShutdown) || (Grid.IsStatic && broadcastDist == 1)) //TODO sort out skipping for static grids too
            {
                broadcastDist = 0; //Zeroing these out so a grid that loses thrusters completely does not get a phantom signal locked to it
                sizeEnum = 0;
                return;
            }
            var ss = ServerSettings.Instance;

            broadcastDistOld = broadcastDist;

            if (ss.IncludeThrustInSignal && !Grid.IsStatic)
            {
                double rawThrustOutput = 0.0d;
                foreach (var thrust in thrustList)
                {
                    var thrustOutput = thrust.Key.CurrentThrust;
                    if (thrustOutput == 0)
                        continue;
                    rawThrustOutput += thrustOutput / thrust.Value;
                }
                broadcastDist = (int)rawThrustOutput;
            }

            if (ss.IncludePowerInSignal)
            {
                double rawPowerOutput = 0.0d;
                foreach (var power in powerList)
                {
                    var powerOutput = power.Key.CurrentOutput; //in MW
                    if (powerOutput == 0)
                        continue;
                    rawPowerOutput += powerOutput / power.Value;
                }
            }

            //Cooldown
            if (broadcastDistOld > broadcastDist || Grid.IsStatic)
            {
                if (gridSize == 0)
                    broadcastDist = (int)(broadcastDistOld * ss.LargeGridCooldownRate);
                else
                    broadcastDist = (int)(broadcastDistOld * ss.SmallGridCooldownRate);
                if (broadcastDist <= 1)
                    broadcastDist = 1;
            }





            //TODO roll these categories to server settings?
            if (broadcastDist < 100)//Idle
            {
                sizeEnum = 0;
            }
            else if (broadcastDist < 8000)//Small
            {
                sizeEnum = 1;
            }
            else if (broadcastDist < 25000)//Medium
            {
                sizeEnum = 2;
            }
            else if (broadcastDist < 100000)//Large
            {
                sizeEnum = 3;
            }
            else if (broadcastDist < 250000)//Huge
            {
                sizeEnum = 4;
            }
            else//Massive
            {
                sizeEnum = 5;
            }

            if (ss.ShutdownPowerOverMaxSignal)//TODO set up shutdown reason enum and a singular list
            {
                if (broadcastDist >= ss.MaxSignalforPowerShutdown && !powerShutdown)
                {
                    sizeEnum = 6;
                    Session.powershutdownList.Add(this);
                }
                else if (broadcastDist < ss.MaxSignalforPowerShutdown && powerShutdown)
                    Session.powershutdownList.Remove(this);
            }
            if (ss.ShutdownThrustersOverMaxSignal)
            {
                if (broadcastDist >= ss.MaxSignalforThrusterShutdown && !thrustShutdown)
                {
                    sizeEnum = 6;
                    Session.thrustshutdownList.Add(this);
                }
                else if (broadcastDist < ss.MaxSignalforThrusterShutdown && thrustShutdown)
                    Session.thrustshutdownList.Remove(this);
            }




            broadcastDistSqr = (long)broadcastDist * broadcastDist;
            return;
        }

        internal void TogglePower()
        {
            foreach (var power in powerList)
                if (power.Key.Enabled && !power.Key.MarkedForClose)
                    power.Key.Enabled = false;
        }
        internal void ToggleThrust()
        {
            foreach (var thrust in thrustList)
                if (thrust.Key.Enabled && !thrust.Key.MarkedForClose)
                    thrust.Key.Enabled = false;
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
