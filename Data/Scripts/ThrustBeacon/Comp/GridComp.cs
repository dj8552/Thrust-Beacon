using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;

namespace ThrustBeacon
{
    internal class GridComp
    {
        internal MyCubeGrid Grid;
        internal Dictionary<IMyThrust, int> thrustList = new Dictionary<IMyThrust, int>();
        internal Dictionary<IMyPowerProducer, int> powerList = new Dictionary<IMyPowerProducer, int>();
        internal List<MyEntity> weaponList = new List<MyEntity>();
        internal List<MyCubeBlock> specials = new List<MyCubeBlock>();
        internal IMyGridGroupData group;

        internal int broadcastDist;
        internal int broadcastDistOld;
        internal long factionID = 0;
        internal VRage.Game.MyCubeSize gridSize;
        internal float coolDownRate = 0f;
        internal float signalRange = 0f;
        internal float detectionRange = 0f;
        internal bool specialsDirty = false;
        internal int funcCount = 0;
        internal bool gridLogging = false;
        internal string gridLog = "";
        
        internal void Init(MyCubeGrid grid, IMyGridGroupData myGroup)
        {
            group = myGroup;
            Grid = grid;
            Grid.OnFatBlockAdded += FatBlockAdded;
            Grid.OnFatBlockRemoved += FatBlockRemoved;
            Grid.OnBlockOwnershipChanged += OnBlockOwnershipChanged;
            gridSize = Grid.GridSizeEnum;
            foreach (var fat in Grid.GetFatBlocks())
            {
                FatBlockAdded(fat);
            }
            RecalcSpecials();
        }

        private void OnBlockOwnershipChanged(MyCubeGrid grid)
        {
            if (grid.BigOwners != null && grid.BigOwners.Count > 0)
            {
                var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(grid.BigOwners[0]);
                factionID = faction == null ? 0 : faction.FactionId;
            }
        }

        internal void FatBlockAdded(MyCubeBlock block)
        {
            //Ownership update
            if (block.CubeGrid.BigOwners != null && block.CubeGrid.BigOwners.Count > 0)
            {
                var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(block.CubeGrid.BigOwners[0]);
                factionID = faction == null ? 0 : faction.FactionId;
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
                    divisor = ServerSettings.Instance.DefaultPowerDivisor;
                powerList.Add(power, divisor);
            }
            else if (thruster != null)
            {
                int divisor;
                if (!Session.SignalProducer.TryGetValue(subTypeID.ToString(), out divisor))
                    divisor = ServerSettings.Instance.DefaultThrustDivisor;
                thrustList.Add(thruster, divisor);
            }
            else if (weapon)
                weaponList.Add(block);

            //Checks if the block is a specialty one that alters signal
            if (Session.BlockConfigs.ContainsKey(subTypeID))
            {
                specialsDirty = true;
                specials.Add(block);
                var func = block as IMyFunctionalBlock;
                if (func != null)
                    func.EnabledChanged += Func_EnabledChanged;                  
            }

            //Functional count, rolls up to the group and is used in weighing the largest faction
            if (block is IMyFunctionalBlock)
            {
                funcCount++;
                if (Session.GroupDict.ContainsKey(group))
                    Session.GroupDict[group].groupFuncCount++;
            }
        }

        //Monitors specialty blocks that alter signal for Enabled changing
        private void Func_EnabledChanged(IMyTerminalBlock obj)
        {
            specialsDirty = true;
            Session.GroupDict[group].groupSpecialsDirty = true;
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
            if (block is IMyFunctionalBlock)
            {
                funcCount--;
                if(Session.GroupDict.ContainsKey(group))
                    Session.GroupDict[group].groupFuncCount--;
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
            specialsDirty = false;
        }

        //Called by the group to refresh the signal output
        internal void CalcSignal()
        {
            var ss = ServerSettings.Instance;
            broadcastDistOld = broadcastDist;
            broadcastDist = 0;
            if (specialsDirty)
                RecalcSpecials();

            //Thrust
            int finalThrust = 0;
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
                finalThrust += (int)rawThrustOutput;
            }

            //Power
            int finalPower = 0;
            if (ss.IncludePowerInSignal)
            {
                double rawPowerOutput = 0.0d;
                foreach (var power in powerList)
                {
                    var powerOutput = power.Key.CurrentOutput * 1000000; //convert MW to W
                    if (powerOutput == 0)
                        continue;
                    rawPowerOutput += powerOutput / power.Value;
                }
                finalPower += (int)rawPowerOutput;
            }

            //WeaponHeat
            int finalWeaponHeat = 0;
            if (ss.IncludeWeaponHeatInSignal && Session.wcAPI.IsReady)
            {
                double rawWepHeat = 0.0d;
                foreach(var wep in weaponList)
                {
                    var wepHeat = Session.wcAPI.GetHeatLevel(wep);
                    if (wepHeat == 0)
                        continue;
                    rawWepHeat += wepHeat / ss.DefaultWeaponHeatDivisor;
                }
                finalWeaponHeat += (int)rawWepHeat;
            }

            //Defense Shields
            int finalShield = 0;
            if (ss.IncludeShieldHPInSignal && Session.dsAPI.IsReady && Session.dsAPI.GridHasShield(Grid))
            {
                finalShield += (int)(Session.dsAPI.GetShieldInfo(Grid).Item3 * 100 / ss.DefaultShieldHPDivisor);
                //Item 3 is charge, mult by 100 for HP
                //Item 6 is heat, 0-100 in increments of 10
            }

            //Final tally
            broadcastDist = finalPower + finalShield + finalThrust + finalWeaponHeat;


            //Cooldown
            if (broadcastDistOld > broadcastDist || Grid.IsStatic)
            {
                //Reworked cooldown to normalize to a per second value, since recalcs are on a variable schedule
                var partialCoolDown = (broadcastDistOld - broadcastDistOld * coolDownRate) * ((float)(Session.Tick - Session.GroupDict[group].groupLastUpdate) / 59);
                broadcastDist = (int)(broadcastDistOld - partialCoolDown);
                
                if (broadcastDist <= 1)
                    broadcastDist = 1;
            }

            //SignalRange increase from specials
            broadcastDist += (int)signalRange;

            //Logging rollup
            if (gridLogging)
            {
                gridLog = "";
                if (broadcastDist > 1)
                    gridLog += $"{Grid.DisplayName} total output: {broadcastDist}m";
                if (finalThrust > 0)
                    gridLog += $"\n    Thrust: {finalThrust}m";
                if (finalPower > 0)
                    gridLog += $"\n    Power: {finalPower}m";
                if (finalWeaponHeat > 0)
                    gridLog += $"\n    Weapon Heat: {finalWeaponHeat}m";
                if (finalShield > 0)
                    gridLog += $"\n    Shields: {finalShield}m";
                if (signalRange != 0)
                    gridLog += $"\n    Signal Range Mod: {signalRange}m";
                if (detectionRange != 0)
                    gridLog += $"\n    Detection Range Mod: {detectionRange}m";
                gridLogging = false;
            }
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
        internal void TogglePowerOn()
        {
            foreach (var power in powerList)
                if (!power.Key.Enabled && !power.Key.MarkedForClose)
                    power.Key.Enabled = true;
        }
        internal void ToggleThrustOn()
        {
            foreach (var thrust in thrustList)
                if (!thrust.Key.Enabled && !thrust.Key.MarkedForClose)
                    thrust.Key.Enabled = true;
        }

        internal void Clean()
        {
            Grid.OnFatBlockAdded -= FatBlockAdded;
            Grid.OnFatBlockRemoved -= FatBlockRemoved;
            Grid.OnBlockOwnershipChanged -= OnBlockOwnershipChanged;

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
        }
    }
}
