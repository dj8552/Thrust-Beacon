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
using System.Diagnostics;

namespace ThrustBeacon
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            Networking.Register();
            if(Client)
                MyAPIGateway.Session.Player.Controller.ControlledEntityChanged += GridChange;
        }
        public override void LoadData()
        {
            MPActive = MyAPIGateway.Multiplayer.MultiplayerActive;
            Server = (MyAPIGateway.Multiplayer.MultiplayerActive && MyAPIGateway.Multiplayer.IsServer) || !MPActive;
            Client = (MyAPIGateway.Multiplayer.MultiplayerActive && !MyAPIGateway.Multiplayer.IsServer) || !MPActive;
            if (Client)
            {
                InitConfig();
                hudAPI = new HudAPIv2(InitMenu);
                viewDist = Math.Min(Session.SessionSettings.SyncDistance, Session.SessionSettings.ViewDistance);
            }
            wcAPI = new WcApi();
            wcAPI.Load();
            if (Server)
            {
                MyEntities.OnEntityCreate += OnEntityCreate;
                LoadSignalProducerConfigs();
                LoadBlockConfigs();
                InitServerConfig();
                List<VRage.Game.MyDefinitionId> tempWeaponDefs = new List<VRage.Game.MyDefinitionId>();
                wcAPI.GetAllCoreWeapons(tempWeaponDefs);
                foreach (var def in tempWeaponDefs)
                {
                    weaponSubtypeIDs.Add(def.SubtypeId);                  
                }
            }

        }

        private void GridChange(VRage.Game.ModAPI.Interfaces.IMyControllableEntity previousEnt, VRage.Game.ModAPI.Interfaces.IMyControllableEntity newEnt)
        {
            if (newEnt is IMyCharacter)
            {
                SignalList.Clear();
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
                if ((!_startBlocks.IsEmpty || !_startGrids.IsEmpty))
                    StartComps();
                foreach (var gridComp in GridList)
                {
                    gridComp.CalcSignal();//TODO: See if there's a better way to account for pulsing/blipping the gas
                }
                //Find player controlled entities in range and broadcast to them
                PlayerList.Clear();

                if (MPActive)
                    MyAPIGateway.Multiplayer.Players.GetPlayers(PlayerList);
                else
                    PlayerList.Add(Session.Player);

                foreach (var player in PlayerList)
                {
                    if (player == null || player.Character == null || (MPActive && player.SteamUserId == 0) || (!ServerSettings.Instance.SendSignalDataToSuits && player.Controller.ControlledEntity is IMyCharacter))
                    {
                        continue;
                    }
                    var playerPos = player.Character.WorldAABB.Center;
                    if (playerPos == Vector3D.Zero)
                    {
                        MyLog.Default.WriteLineAndConsole($"Player position error - Vector3D.Zero - player.Name: {player.DisplayName} - player.SteamUserId: {player.SteamUserId}");
                        continue;
                    }

                    var controlledEnt = player.Controller?.ControlledEntity?.Entity?.Parent?.EntityId;
                    var block = player.Controller?.ControlledEntity?.Entity as IMyCubeBlock;
                    GridComp playerComp = null;
                    var playerGridDetectionModSqr = 0f;
                    var playerGridAccuracyMod = 0f;
                    if (block != null && GridListSpecials.TryGetValue(block.CubeGrid, out playerComp))
                    {
                        playerGridDetectionModSqr = playerComp.detectionRange * playerComp.detectionRange;
                        playerGridAccuracyMod = playerComp.detectionAccuracy;
                    }

                    var playerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId);
                    var tempList = new List<SignalComp>();
                    foreach (var grid in GridList)
                    {
                        var playerGrid = grid.Grid.EntityId == controlledEnt;
                        if (!playerGrid && grid.broadcastDist < 2) continue;
                        var gridPos = grid.Grid.PositionComp.WorldAABB.Center;
                        var distToTargSqr = Vector3D.DistanceSquared(playerPos, gridPos);
                        if (playerGrid || distToTargSqr <= grid.broadcastDistSqr + playerGridDetectionModSqr)
                        {
                            var signalData = new SignalComp();
                            signalData.position = (Vector3I)gridPos;
                            signalData.range = playerGrid ? grid.broadcastDist : (int)Math.Sqrt(distToTargSqr);
                            signalData.faction = grid.faction;
                            signalData.entityID = grid.Grid.EntityId;
                            signalData.sizeEnum = grid.sizeEnum;
                            //signalData.accuracy = TODO calc this
                            if (!playerGrid && playerFaction != null)
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

            }

            //Clientside list processing
            if (Client && Tick % 59 == 0 && Settings.Instance.hideWC)
            {
                entityIDList.Clear();
                var controlledEnt = MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity?.Parent;
                if (controlledEnt != null && controlledEnt is MyCubeGrid)
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
            }


            if (Server && Tick % 5 == 0 && powershutdownList.Count > 0)//5 tick interval to keep players from spamming keys to turn power back on
            {
                foreach (var gridComp in powershutdownList.ToArray())
                    gridComp.TogglePower();
            }
            if (Server && Tick % 5 == 0 && thrustshutdownList.Count > 0)//5 tick interval to keep players from spamming keys to turn power back on
            {
                foreach (var gridComp in thrustshutdownList.ToArray())
                    gridComp.ToggleThrust();
            }

        }

        //TODO tie in accuracy to jitter
        public float ComputeSignalStrength(SignalComp contact, float distance)
        {
            float maxJitterDistance = 400000f;
            float f = distance / maxJitterDistance;
            return 1.0f - Math.Min(f, 1.0f);
        }

        public Vector3I GetRandomJitter(SignalComp contact, Vector3 camPos)
        {
            int tickRate = 60;
            float minimumJitterCutoff = 0.25f;
            float maxJitterAmount = contact.accuracy;

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
            if (Client && hudAPI.Heartbeat && SignalList.Count > 0 && MyAPIGateway.Session.Config.HudState != 0 && !MyAPIGateway.Gui.IsCursorVisible)
            {
                var s = Settings.Instance;
                var viewProjectionMat = Session.Camera.ViewMatrix * Session.Camera.ProjectionMatrix;
                var camPos = Session.Camera.Position;
                var playerEnt = MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity?.Parent?.EntityId;

                /*
                foreach (var newSignal in NewSignalList.ToArray())
                {
                    var contact = newSignal.Value.Item1;
                    if (contact.entityID == playerEnt)
                    {
                        NewSignalList.Remove(newSignal.Key);
                        continue;
                    }
                    var contactAge = Tick - newSignal.Value.Item2;

                    if (contactAge >= newTimeTicks)
                    {
                        NewSignalList.Remove(newSignal.Key);
                        continue;
                    }
                    
                    //Newly discovered signal AV shenanigans go here
                }
                */

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
                        //if (NewSignalList.ContainsKey(contact.entityID)) continue;
                        var contactAge = Tick - signal.Value.Item2;
                        if (contactAge >= stopDisplayTimeTicks)
                        {
                            if (contactAge >= keepTimeTicks)
                                SignalList.Remove(signal.Key);
                            continue;
                        }
                        float distance = Vector3.Distance(contact.position, camPos);
                        if (distance < s.hideDistance) continue;

                        var baseColor = contact.relation == 1 ? s.enemyColor : contact.relation == 3 ? s.friendColor : s.neutralColor;
                        var adjColor = baseColor;
                        if (fadeTimeTicks > 0)
                        {
                            byte colorFade = (byte)(contactAge < fadeTimeTicks ? 0 : (contactAge - fadeTimeTicks) / 2);
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
                            var labelPosition = new Vector2D(screenCoords.X + (s.symbolWidth * 0.25), screenCoords.Y + (symbolHeight * 0.4));
                            var dispSize = contact.range > 1000 ? (contact.range / 1000).ToString("0.#") + " km" : contact.range.ToString("0.#") + " m";
                            var dispRange = distance > 1000 ? (distance / 1000).ToString("0.#") + " km" : distance.ToString("0.#") + " m";
                            //var info = new StringBuilder(contact.faction + " " + dispSize + " sig " + "\n" + dispRange); //Testing alternate display
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
                MyAPIGateway.Session.Player.Controller.ControlledEntityChanged -= GridChange;
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
            NewSignalList.Clear();
            threatList.Clear();
            obsList.Clear();
            powershutdownList.Clear();
            thrustshutdownList.Clear();
        }
    }
}
