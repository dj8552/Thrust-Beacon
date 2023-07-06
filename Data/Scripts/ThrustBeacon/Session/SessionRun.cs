using CoreSystems.Api;
using Digi.Example_NetworkProtobuf;
using Draygo.API;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;

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
        HudAPIv2 hudAPI;
        WcApi wcAPI;
        public Networking Networking = new Networking(1337); //TODO: Pick a new number based on mod ID
        internal MyStringId symbol = MyStringId.GetOrCompute("FrameSignal");
        internal float symbolWidth = 0.02f;
        internal float symbolHeight = 0f;//Leave this as zero, monitor aspect ratio is figured in later
        internal Vector4 color = Color.Red.ToVector4();




        public override void BeforeStart()
        {
            Networking.Register();
        }
        public override void LoadData()
        {
            IsServer = MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Session.IsServer;
            DedicatedServer = MyAPIGateway.Utilities.IsDedicated;
            MpActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            IsClient = !IsServer && !DedicatedServer;
            IsHost = IsServer && !DedicatedServer && MpActive;
            MpServer = IsHost || DedicatedServer || !MpActive;
            if (!IsClient)
            {
                MyEntities.OnEntityCreate += OnEntityCreate;
                //TODO: Hook player joining for server and populate PlayerList?

            }
            else
            {
                hudAPI = new HudAPIv2();
                wcAPI = new WcApi();
                wcAPI.Load();
            }

        }
        public override void UpdateBeforeSimulation()
        {
            if (symbolHeight == 0)
            {
                var aspectRatio = Session.Camera.ViewportSize.X / Session.Camera.ViewportSize.Y;
                symbolHeight = symbolWidth * aspectRatio;
            }

            Tick++;
            if (Tick % 60 == 0 && !IsClient)
            {
                foreach (var gridComp in GridList)
                {
                    if (gridComp.thrustList.Count > 0)
                        gridComp.CalcThrust();
                }

                //Find player controlled entities in range and broadcast to them
                foreach (var player in PlayerList)
                {
                    var playerPos = player.GetPosition();
                    var playerSteamID = player.SteamUserId;
                    if (playerPos == null || playerPos == Vector3D.Zero || playerSteamID == 0) continue;
                    var tempList = new List<SignalComp>();
                    foreach (var grid in GridList)
                    {
                        if (grid.broadcastDist <= 50) continue; //Cull short ranges with practically zero chance of being seen
                        var gridPos = grid.Grid.PositionComp.WorldAABB.Center;
                        if (Vector3D.DistanceSquared(playerPos, gridPos) <= grid.broadcastDistSqr)
                        {
                            var signalData = new SignalComp();
                            signalData.position = gridPos;
                            signalData.range = (int)(Vector3D.Distance(playerPos, gridPos));
                            signalData.message = grid.broadcastMsg;
                            signalData.entityID = grid.Grid.EntityId;
                            tempList.Add(signalData);
                        }
                    }
                    if (tempList.Count > 0)
                        Networking.SendToPlayer(new PacketBase(tempList), playerSteamID);
                }
                if (!_startBlocks.IsEmpty || !_startGrids.IsEmpty)
                StartComps();              
            }
            else if (Tick % 60 == 0 && IsClient)
            {
                //TODO: Client side list filtering to deconflict items in WC range
                //Add desired signals to DrawList

                //temp force feeding without filtering and sample points
                DrawList.Clear();
                foreach (var temp in SignalList)
                    DrawList.Add(temp);
                var temp1 = new SignalComp() { message = "Test1", range = 1234, position = new Vector3D(1000,2000,3000), entityID = 0 };
                var temp2 = new SignalComp() { message = "Test2", range = 4567000, position = new Vector3D(11000, 2000, 3000), entityID = 0 };
                DrawList.Add(temp1);
                DrawList.Add(temp2);
                SignalList.Clear();
            }
        }

        public override void Draw()
        {
            if (IsClient && hudAPI.Heartbeat)
            {
                var Up = MyAPIGateway.Session.Camera.WorldMatrix.Up;
                var adjSymbolHeight = symbolHeight / 70 * MyAPIGateway.Session.Camera.FieldOfViewAngle;

                foreach (var signal in DrawList)
                {
                    var varPos = signal.position;
                    var screenCoords = Session.Camera.WorldToScreen(ref varPos);
                    if (screenCoords.Z >= 1) continue; //TODO: Signal is off screen

                    var symbolPosition = new Vector2D(screenCoords.X, screenCoords.Y);
                    var labelPosition = new Vector2D(screenCoords.X + (symbolHeight * 0.4), screenCoords.Y + (symbolHeight * 0.5));
                    var dispRange = signal.range > 1000 ? signal.range / 1000 + " km" : signal.range + " m";
                    var info = new StringBuilder(signal.message + "\n" + dispRange);
                    var Label = new HudAPIv2.HUDMessage(info, labelPosition, new Vector2D(0,-0.001), 2, 1, true, true);
                    Label.InitialColor = Color.Red;
                    Label.Visible = true;
                    var symbolObj = new HudAPIv2.BillBoardHUDMessage(symbol, symbolPosition, Color.Red, Width: symbolWidth, Height: symbolHeight, TimeToLive: 2);
               
                }
            }
        }
        protected override void UnloadData()
        {
            if (MpServer)
            {
                MyEntities.OnEntityCreate -= OnEntityCreate;
                Clean();
            }
            if (wcAPI != null)
                wcAPI.Unload();
            if (hudAPI != null)
                hudAPI.Unload();
            Networking?.Unregister();
            Networking = null;
            PlayerList.Clear();
            GridList.Clear();
            SignalList.Clear();
            DrawList.Clear();
        }
    }
}
