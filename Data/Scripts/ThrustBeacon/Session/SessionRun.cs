using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Diagnostics;
using VRage.Game.Components;

namespace ThrustBeacon
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        internal static int Tick;
        internal bool IsServer;
        internal bool IsClient;
        internal bool IsDedicated;
        internal bool DedicatedServer;
        internal bool MpActive;
        internal bool MpServer;
        internal bool IsHost;

        public override void LoadData()
        {
            IsServer = MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Session.IsServer;
            DedicatedServer = MyAPIGateway.Utilities.IsDedicated;
            MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            IsClient = !IsServer && !DedicatedServer;// && MpActive;
            IsHost = IsServer && !DedicatedServer && MpActive;
            MpServer = IsHost || DedicatedServer || !MpActive;
            if (MpServer)
                MyEntities.OnEntityCreate += OnEntityCreate;
        }
        public override void UpdateBeforeSimulation()
        {
            Tick++;
            if (Tick % 60 == 0 && MpServer)
            {
                var timer = new Stopwatch();//temp
                timer.Start();//temp
                foreach (var gridComp in GridList)
                {
                    if (gridComp.thrustList.Count > 0)
                        gridComp.CalcThrust();
                    if (gridComp.broadcastDistOld == gridComp.broadcastDist)
                        continue;
                    foreach (IMyBeacon beacon in gridComp.beaconList)
                    {
                        beacon.Radius = gridComp.broadcastDist;
                        beacon.HudText = gridComp.broadcastMsg;
                    }
                    
                }
                timer.Stop();//temp
                MyAPIGateway.Utilities.ShowNotification("Runtime: " + timer.Elapsed);



                if (!_startBlocks.IsEmpty || !_startGrids.IsEmpty)
                StartComps();
            }           
        }

        protected override void UnloadData()
        {
            if (MpServer)
            {
                MyEntities.OnEntityCreate -= OnEntityCreate;
                Clean();
            }
        }


    }
}
