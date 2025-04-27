using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Interfaces;

namespace ThrustBeacon
{
    public class ThrustBeaconPBApi
    {
        private Sandbox.ModAPI.Ingame.IMyTerminalBlock _self;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, int> _getThrustSignalBroadcastRange;
        private Func<Sandbox.ModAPI.Ingame.IMyTerminalBlock, bool> _registerPB;


        /// <summary>
        /// Activates the ThrustBeaconAPI using <see cref="IMyTerminalBlock"/> <paramref name="pbBlock"/>.
        /// </summary>
        /// <remarks>
        /// Recommended to use 'Me' in <paramref name="pbBlock"/> for simplicity.
        /// </remarks>
        /// <param name="pbBlock"></param>
        /// <returns><see cref="true"/>  if all methods assigned correctly, <see cref="false"/>  otherwise</returns>
        /// <exception cref="Exception">Throws exception if ThrustBeacon is not present</exception>
        public bool Activate(Sandbox.ModAPI.Ingame.IMyTerminalBlock pbBlock)
        {
            var dict = pbBlock.GetProperty("ThrustBeaconAPI")?.As<Dictionary<string, Delegate>>().GetValue(pbBlock);
            if (dict == null) throw new Exception("ThrustBeaconAPI failed to activate");
            _self = pbBlock;
            return ApiAssign(dict);
        }

        public bool ApiAssign(IReadOnlyDictionary<string, Delegate> delegates)
        {
            if (delegates == null)
                return false;

            AssignMethod(delegates, "TBRegisterPB", ref _getThrustSignalBroadcastRange);
            AssignMethod(delegates, "TBGetThrustSignalBroadcastRange", ref _getThrustSignalBroadcastRange);
            return true;
        }

        private void AssignMethod<T>(IReadOnlyDictionary<string, Delegate> delegates, string name, ref T field) where T : class
        {
            if (delegates == null)
            {
                field = null;
                return;
            }

            Delegate del;
            if (!delegates.TryGetValue(name, out del))
                throw new Exception($"{GetType().Name} :: Couldn't find {name} delegate of type {typeof(T)}");

            field = del as T;
            if (field == null)
                throw new Exception(
                    $"{GetType().Name} :: Delegate {name} is not type {typeof(T)}, instead it's: {del.GetType()}");
        }

        /// <summary>
        /// Returns the most recent calculated broadcast range of the grid group the PB is in.
        /// </summary>
        /// <returns><see cref="int"/> Signal broadcast range in meters (-2 if block is not registered, -1 if updated value is not ready) </returns>
        public int GetThrustSignalBroadcastRange() => _getThrustSignalBroadcastRange?.Invoke(_self) ?? -1;

        /// <summary>
        /// Registers programmable block to function with Thrust Beacon API.
        /// </summary>
        /// <returns><see cref="bool"/> Successfully registered </returns>
        public bool RegisterPB() => _registerPB?.Invoke(_self) ?? false;
    }
}