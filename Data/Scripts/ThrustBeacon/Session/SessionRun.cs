using CoreSystems.Api;
using Digi.Example_NetworkProtobuf;
using Draygo.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.Game;
using VRage;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using VRage.Game.ModAPI;

namespace ThrustBeacon
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            Networking.Register();
        }
        public override void LoadData()
        {
            MPActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            Server = (MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Multiplayer.IsServer) || !MPActive; //TODO check if I jacked these up for actual application
            Client = (MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer) || !MPActive;
            if (Server)
            {
                MyEntities.OnEntityCreate += OnEntityCreate;
            }
            if (Client)
            {
                InitConfig();
                hudAPI = new HudAPIv2(InitMenu);
                wcAPI = new WcApi();
                wcAPI.Load();
                viewDist = Math.Min(Session.SessionSettings.SyncDistance, Session.SessionSettings.ViewDistance);
            }
        }



        public override void UpdateBeforeSimulation()
        {
            if (Client && symbolHeight == 0)//TODO see if there's a better spot for this that only runs once... seems like Camera isn't available in LoadData
            {
                aspectRatio = Session.Camera.ViewportSize.X / Session.Camera.ViewportSize.Y;
                symbolHeight = Settings.Instance.symbolWidth * aspectRatio;
                offscreenHeight = Settings.Instance.offscreenWidth * aspectRatio;
            }

            Tick++;
            if (Server && Tick % 60 == 0)
            {
                foreach (var gridComp in GridList)
                {
                    if (gridComp.thrustList.Count > 0)
                        gridComp.CalcThrust();//TODO: See if there's a better way to account for pulsing/blipping the gas
                }
                //Find player controlled entities in range and broadcast to them
                PlayerList.Clear();

                if (MPActive)
                    MyAPIGateway.Multiplayer.Players.GetPlayers(PlayerList);
                else
                    PlayerList.Add(Session.Player);

                foreach (var player in PlayerList)
                {
                    if (player == null || player.Character == null || (MPActive && player.SteamUserId == 0))
                        continue; 

                    var playerPos = player.Character.WorldAABB.Center;
                    if (playerPos == Vector3D.Zero)
                    {
                        MyLog.Default.WriteLineAndConsole($"Player position error - Vector3D.Zero - player.Name: {player.DisplayName} - player.SteamUserId: {player.SteamUserId}");
                        continue;
                    }
                    var controlledEnt = player.Controller?.ControlledEntity?.Entity?.Parent?.EntityId;
                    var playerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId);
                    var tempList = new List<SignalComp>();
                    foreach (var grid in GridList)
                    {
                        var playerGrid = grid.Grid.EntityId == controlledEnt;
                        if (!playerGrid && grid.broadcastDist == 0) continue;
                        var gridPos = grid.Grid.PositionComp.WorldAABB.Center;
                        var distToTargSqr = Vector3D.DistanceSquared(playerPos, gridPos);
                        if (playerGrid || distToTargSqr <= grid.broadcastDistSqr)
                        {
                            var signalData = new SignalComp();
                            signalData.position = (Vector3I)gridPos;
                            signalData.range = playerGrid ? grid.broadcastDist : (int)Math.Sqrt(distToTargSqr);
                            signalData.faction = grid.faction;
                            signalData.entityID = grid.Grid.EntityId;
                            signalData.sizeEnum = grid.sizeEnum;
                            if (false || playerFaction != null)
                            {
                                var relation = MyAPIGateway.Session.Factions.GetRelationBetweenFactions(playerFaction.FactionId, grid.factionID);
                                signalData.relation = (byte)relation;
                            }
                            else
                                signalData.relation = 0;
                            tempList.Add(signalData);

                            if(!MPActive)
                            {
                                if (SignalList.ContainsKey(signalData.entityID))
                                {
                                    var updateTuple = new MyTuple<SignalComp, int>(signalData, Tick);
                                    SignalList[signalData.entityID] = updateTuple;
                                }
                                else
                                    SignalList.TryAdd(signalData.entityID, new MyTuple<SignalComp, int>(signalData, Tick));
                            }
                        }
                    }
                    if (MPActive && tempList.Count > 0)
                        Networking.SendToPlayer(new PacketBase(tempList), player.SteamUserId);
                }
                if ((!_startBlocks.IsEmpty || !_startGrids.IsEmpty))
                    StartComps();
            }

            //Clientside list processing
            if (Client && Tick % 60 == 0)
            {
                entityIDList.Clear();
                var controlledEnt = MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity?.Parent;
                if (controlledEnt != null && controlledEnt is MyCubeGrid)//WC Deconflict
                {
                    var myEnt = (MyEntity)controlledEnt;
                    wcAPI.GetSortedThreats(myEnt, threatList);
                    foreach (var item in threatList)
                    {
                        entityIDList.Add(item.Item1.EntityId);
                    }
                    wcAPI.GetObstructions(myEnt, obsList);
                    foreach (var item in obsList)
                    {
                        entityIDList.Add(item.EntityId);
                    }
                    foreach (var wcContact in entityIDList)
                    {
                        if (SignalList.ContainsKey(wcContact))
                            SignalList.Remove(wcContact);
                    }
                }

                //temp sample points
                if (Tick % 600 == 0 && !SignalList.ContainsKey(0) && !SignalList.ContainsKey(1))
                {

                    var temp1 = new MyTuple<SignalComp, int>(new SignalComp() { faction = "Mover (won't fade for a real one)", range = 1234, position = new Vector3I(1000, 2000, 3000), entityID = 123, sizeEnum = 3, relation = 0 }, Tick);
                    var temp2 = new MyTuple<SignalComp, int>(new SignalComp() { faction = "Lost Signal", range = 4567000, position = new Vector3I(11000, 2000, 3000), entityID = 456, sizeEnum = 2, relation = 1 }, Tick);
                    SignalList.TryAdd(0, temp1);
                    SignalList.TryAdd(1, temp2);
                }

                if (SignalList.ContainsKey(2)) SignalList.Remove(2);
                var temp3 = new MyTuple<SignalComp, int>(new SignalComp() { faction = "Norm Update", range = 4567000, position = new Vector3I(500000, 2000, 3000), entityID = 789, sizeEnum = 4, relation = 3 }, Tick);
                SignalList.TryAdd(2, temp3);
                //temp moving point for positional update tests
                if (SignalList.ContainsKey(0)) SignalList[0].Item1.position += new Vector3I(100, 0, 0);
                //end of temp
            }


            if (Server && Tick % 5 == 0 && shutdownList.Count > 0)//5 tick interval to keep players from spamming keys to turn power back on
            {
                foreach (var gridComp in shutdownList.ToArray())
                    gridComp.TogglePower();
            }

        }

        public float ComputeSignalStrength(SignalComp contact, float distance)
        {
            float maxJitterDistance = 400000f;
            float f = distance / maxJitterDistance;
            // TODO - Add in a way to calculate signal strength, and boost it if the piloted grid has antenna(s)/tech
            return 1.0f - Math.Min(f, 1.0f);
        }

        public Vector3I GetRandomJitter(SignalComp contact, Vector3 camPos)
        {
            int tickRate = 20;
            float minimumJitterCutoff = 0.25f;
            float maxJitterAmount = 6000;

            float distance = Vector3.Distance(contact.position, camPos);
            Random random = new Random((int)contact.entityID + (Tick / tickRate));
            float amount = 1.0f - ComputeSignalStrength(contact, distance);
            amount = amount < minimumJitterCutoff ? 0.0f : amount;

            int jitter = (int)MathHelper.Lerp(0, maxJitterAmount, amount * amount);
            int x = random.Next(jitter * 2) - jitter;
            int y = random.Next(jitter * 2) - jitter;
            int z = random.Next(jitter * 2) - jitter;
            Vector3I offset = new Vector3I(x, y, z);

            return offset;
        }

        public override void Draw()
        {
            if (Client && hudAPI.Heartbeat && SignalList.Count > 0)
            {
                var s = Settings.Instance;
                var viewProjectionMat = Session.Camera.ViewMatrix * Session.Camera.ProjectionMatrix;
                var camPos = Session.Camera.Position;
                var playerEnt = MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity?.Parent?.EntityId;

                foreach (var signal in SignalList.ToArray())
                {
                    var contact = signal.Value.Item1;
                    if (contact.entityID == playerEnt)
                    {
                        var dispRange = contact.range > 1000 ? (contact.range / 1000f).ToString("0.#") + " km" : contact.range + " m";
                        var info = new StringBuilder("Broadcast Dist: " + dispRange + "\n" + "Size: " + messageList[contact.sizeEnum]);
                        var Label = new HudAPIv2.HUDMessage(info, s.signalDrawCoords, null, 2, s.textSizeOwn, true, true);
                        Label.Visible = true;
                    }
                    else
                    {
                        var contactAge = Tick - signal.Value.Item2;
                        if (contactAge >= s.maxContactAge)
                        {
                            SignalList.Remove(signal.Key);
                            continue;
                        }
                        var baseColor = contact.relation == 1 ? s.enemyColor : contact.relation == 3 ? s.friendColor : s.neutralColor;
                        var adjColor = baseColor;
                        if (s.fadeOutTime > 0)
                        {
                            byte colorFade = (byte)(contactAge < s.fadeOutTime ? 0 : (contactAge - s.fadeOutTime) / 2);
                            adjColor.R = (byte)MathHelper.Clamp(baseColor.R - colorFade, 0, 255);
                            adjColor.G = (byte)MathHelper.Clamp(baseColor.G - colorFade, 0, 255);
                            adjColor.B = (byte)MathHelper.Clamp(baseColor.B - colorFade, 0, 255);
                        }

                        var contactPosition = contact.position + GetRandomJitter(contact, camPos);
                        var adjustedPos = camPos + Vector3D.Normalize((Vector3D)contactPosition - camPos) * viewDist;
                        var screenCoords = Vector3D.Transform(adjustedPos, viewProjectionMat);
                        var offScreen = screenCoords.X > 1 || screenCoords.X < -1 || screenCoords.Y > 1 || screenCoords.Y < -1 || screenCoords.Z > 1;
                        if (!offScreen)
                        {
                            var symbolPosition = new Vector2D(screenCoords.X, screenCoords.Y);
                            var labelPosition = new Vector2D(screenCoords.X + (symbolHeight * 0.4), screenCoords.Y + (symbolHeight * 0.5));
                            float distance = Vector3.Distance(contact.position, camPos);
                            var dispRange = distance > 1000 ? (distance / 1000).ToString("0.#") + " km" : distance.ToString("0.#") + " m";
                            var info = new StringBuilder(contact.faction + messageList[contact.sizeEnum] + "\n" + dispRange);
                            var Label = new HudAPIv2.HUDMessage(info, labelPosition, new Vector2D(0, -0.001), 2, s.textSize, true, true);
                            Label.InitialColor = adjColor;
                            Label.Visible = true;
                            var symbolObj = new HudAPIv2.BillBoardHUDMessage(symbolList[contact.sizeEnum], symbolPosition, adjColor, Width: s.symbolWidth, Height: symbolHeight, TimeToLive: 2, HideHud: true, Shadowing: true);
                        }
                        else
                        {
                            if (screenCoords.Z > 1)//Camera is between player and target
                                screenCoords *= -1;
                            var vectorToPt = new Vector2D(screenCoords.X, screenCoords.Y);
                            vectorToPt.Normalize();
                            vectorToPt *= offscreenSquish;

                            var rotation = (float)Math.Atan2(screenCoords.X, screenCoords.Y);
                            var symbolObj = new HudAPIv2.BillBoardHUDMessage(symbolOffscreenArrow, vectorToPt, adjColor, Width: s.offscreenWidth * 0.75f, Height: offscreenHeight, TimeToLive: 2, Rotation: rotation, HideHud: true, Shadowing: true);
                            var symbolObj2 = new HudAPIv2.BillBoardHUDMessage(symbolList[contact.sizeEnum], vectorToPt, adjColor, Width: s.symbolWidth, Height: symbolHeight, TimeToLive: 2, HideHud: true, Shadowing: true);
                        }
                    }
                }
            }
        }

        protected override void UnloadData()
        {
            if (Server)
            {
                MyEntities.OnEntityCreate -= OnEntityCreate;       
                Clean();
            }
            if(Client)
            {
                Save(Settings.Instance);
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
            threatList.Clear();
            obsList.Clear();
            shutdownList.Clear();
        }
    }
}
