using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace ThrustBeacon
{
    internal class GridComp
    {
        internal MyCubeGrid Grid;
        internal Dictionary<IMyThrust, int> thrustList = new Dictionary<IMyThrust, int>();
        internal Dictionary<IMyPowerProducer, int> powerList = new Dictionary<IMyPowerProducer, int>();
        internal List<MyEntity> weaponList = new List<MyEntity>();
        internal List<MyCubeBlock> specials = new List<MyCubeBlock>();

        internal int broadcastDist;
        internal int broadcastDistOld;
        internal long broadcastDistSqr;
        internal string faction = "";
        internal long factionID = 0;
        internal VRage.Game.MyCubeSize gridSize;
        internal byte sizeEnum;
        internal float coolDownRate = 0f;
        internal float signalRange = 0f;
        internal float detectionRange = 0f;
        internal bool specialsDirty = false;
        internal int lastUpdate = 0;
        
        internal void Init(MyCubeGrid grid)
        {
            Grid = grid;
            Grid.OnFatBlockAdded += FatBlockAdded;
            Grid.OnFatBlockRemoved += FatBlockRemoved;
            gridSize = Grid.GridSizeEnum;
            RecalcSpecials();
        }

        internal void FatBlockAdded(MyCubeBlock block)
        {
            //Ownership update
            if(block.CubeGrid.BigOwners != null && block.CubeGrid.BigOwners.Count > 0)
            {
                var curFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(block.CubeGrid.BigOwners[0]);
                if (curFaction != null)
                {
                    faction = curFaction.Tag + ".";
                    factionID = curFaction.FactionId;
                }
            }

            var subTypeID = block.BlockDefinition.Id.SubtypeId;
            var power = block as IMyPowerProducer;
            var thruster = block as IMyThrust;
            var weapon = Session.weaponSubtypeIDs.Contains(subTypeID);

            //Checks if block is a signal producer or weapon and adds it to the appropriate list
            if (power != null)
            {
                int divisor;
                if (!Session.SignalProducer.TryGetValue(subTypeID.ToString(), out divisor))
                {
                    divisor = ServerSettings.Instance.DefaultPowerDivisor;
                }
                powerList.Add(power, divisor);
            }
            else if (thruster != null)
            {
                int divisor;
                if (!Session.SignalProducer.TryGetValue(subTypeID.ToString(), out divisor))
                {
                    divisor = ServerSettings.Instance.DefaultThrustDivisor;
                }
                thrustList.Add(thruster, divisor);
            }
            else if (weapon)
            {
                weaponList.Add(block);
            }

            //Checks if the block is a specialty one that alters signal
            if (Session.BlockConfigs.ContainsKey(subTypeID))
            {
                specialsDirty = true;
                specials.Add(block);
                var func = block as IMyFunctionalBlock;
                if (func != null)
                    func.EnabledChanged += Func_EnabledChanged;                  
            }
        }

        //Monitors specialty blocks that alter signal for Enabled changing
        private void Func_EnabledChanged(IMyTerminalBlock obj)
        {
            specialsDirty = true;
        }

        internal void FatBlockRemoved(MyCubeBlock block)
        {
            var thrust = block as IMyThrust;
            var power = block as IMyPowerProducer;
            var weapon = Session.weaponSubtypeIDs.Contains(block.BlockDefinition.Id.SubtypeId);

            if (thrust != null)
                thrustList.Remove(thrust);
            else if (power != null)
                powerList.Remove(power);
            else if (weapon)
                weaponList.Remove(block);
            if (Session.BlockConfigs.ContainsKey(block.BlockDefinition.Id.SubtypeId))
            {
                specialsDirty = true;
                specials.Remove(block);
                var func = block as IMyFunctionalBlock;
                if (func != null)
                    func.EnabledChanged -= Func_EnabledChanged;
            }
        }

        //Update modifiers from specialty blocks
        internal void RecalcSpecials()
        {
            if (gridSize == 0)
                coolDownRate = ServerSettings.Instance.LargeGridCooldownRate;
            else
                coolDownRate = ServerSettings.Instance.SmallGridCooldownRate;
            detectionRange = 0;
            signalRange = 0;

            foreach (var special in specials)
            {
                var func = special as IMyFunctionalBlock;
                var active = func == null || func.Enabled;
                if (!active) continue;
                var cfg = Session.BlockConfigs[special.BlockDefinition.Id.SubtypeId];
                coolDownRate += cfg.SignalCooldown;
                detectionRange += cfg.DetectionRange;
                signalRange += cfg.SignalRange;
            }

            if(specials.Count > 0)
            {
                if (!Session.GridListSpecials.ContainsKey(Grid))
                    Session.GridListSpecials.Add(Grid, this);
                else
                    Session.GridListSpecials[Grid] = this;
            }
            else
                Session.GridListSpecials.Remove(Grid);

            specialsDirty = false;
        }

        //Called by the server to refresh the signal output before checking it against clients in range
        internal void CalcSignal()
        {
            var ss = ServerSettings.Instance;

            broadcastDistOld = broadcastDist;
            broadcastDist = 0;

            if (specialsDirty)
                RecalcSpecials();

            //Thrust
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
                broadcastDist += (int)rawThrustOutput;
            }

            //Power
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
                broadcastDist += (int)rawPowerOutput;
            }

            //WeaponHeat
            if (ss.IncludeWeaponHeatInSignal && Session.wcAPI.IsReady)
            {
                double rawWepHeat = 0.0d;
                foreach(var wep in weaponList)
                {
                    rawWepHeat += Session.wcAPI.GetHeatLevel(wep);
                }
                rawWepHeat /= ss.DefaultWeaponHeatDivisor;
                broadcastDist += (int)rawWepHeat;
            }

            //Defense Shields
            if (ss.IncludeShieldHPInSignal && Session.dsAPI.IsReady && Session.dsAPI.GridHasShield(Grid))
            {
                broadcastDist += (int)(Session.dsAPI.GetShieldInfo(Grid).Item3 * 100 / ss.DefaultShieldHPDivisor);
                //Item 3 is charge, mult by 100 for HP
                //Item 6 is heat, 0-100 in increments of 10
            }

            //Cooldown
            if (broadcastDistOld > broadcastDist || Grid.IsStatic)
            {
                //Reworked cooldown to normalize to a per second value, since recalcs are on a variable schedule
                var partialCoolDown = (broadcastDistOld - broadcastDistOld * coolDownRate) * ((float)(Session.Tick - lastUpdate) / 59);
                broadcastDist = (int)(broadcastDistOld - partialCoolDown);
                
                if (broadcastDist <= 1)
                    broadcastDist = 1;
            }

            //SignalRange increase from specials
            broadcastDist += (int)signalRange;

            //TODO roll these categories to server settings?
            if (broadcastDist < 2500)//Idle
            {
                sizeEnum = 0;
            }
            else if (broadcastDist < 100000)//Small
            {
                sizeEnum = 1;
            }
            else if (broadcastDist < 200000)//Medium
            {
                sizeEnum = 2;
            }
            else if (broadcastDist < 300000)//Large
            {
                sizeEnum = 3;
            }
            else if (broadcastDist < 400000)//Huge
            {
                sizeEnum = 4;
            }
            else//Massive
            {
                sizeEnum = 5;
            }

            //Analytics and time updates
            Session.aUpdateQty++;
            Session.aUpdateTime += Session.Tick - lastUpdate;
            lastUpdate = Session.Tick;

            //Shutdown condition checks
            if (ss.ShutdownPowerOverMaxSignal)
            {
                if (broadcastDist >= ss.MaxSignalforPowerShutdown)
                {
                    sizeEnum = 6;
                    Session.powershutdownList.Add(this);
                }
                else if (broadcastDist < ss.MaxSignalforPowerShutdown)
                    Session.powershutdownList.Remove(this);

            }
            if (ss.ShutdownThrustersOverMaxSignal)
            {
                if (broadcastDist >= ss.MaxSignalforThrusterShutdown)
                {
                    sizeEnum = 6;
                    Session.thrustshutdownList.Add(this);
                }
                else if (broadcastDist < ss.MaxSignalforThrusterShutdown)
                    Session.thrustshutdownList.Remove(this);
            }

            broadcastDistSqr = (long)broadcastDist * broadcastDist;
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
            foreach(var s in specials)
            {
                var func = s as IMyFunctionalBlock;
                if (func != null)
                    func.EnabledChanged -= Func_EnabledChanged;
            }
            specials.Clear();
            broadcastDist = 0;
            broadcastDistSqr = 0;
        }
    }
}
