using CoreSystems.Api;
using Digi.Example_NetworkProtobuf;
using Draygo.API;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Utils;
using VRageMath;
using VRage.Game.ModAPI;
using DefenseShields;

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
            Server = (MPActive && MyAPIGateway.Multiplayer.IsServer) || !MPActive;
            Client = (MPActive && !MyAPIGateway.Multiplayer.IsServer) || !MPActive;
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
                dsAPI = new ShieldApi();
                dsAPI.Load();
                MyEntities.OnEntityCreate += OnEntityCreate;
                LoadSignalProducerConfigs(); //Blocks that generate signal (thrust, power)
                LoadBlockConfigs(); //Blocks that alter targeting
                InitServerConfig(); //Overall settings

                //Roll subtype IDs of all WC weapons into a hash set
                List<VRage.Game.MyDefinitionId> tempWeaponDefs = new List<VRage.Game.MyDefinitionId>();               
                if(wcAPI != null) 
                    wcAPI.GetAllCoreWeapons(tempWeaponDefs);
                foreach (var def in tempWeaponDefs)
                {
                    weaponSubtypeIDs.Add(def.SubtypeId);
                    MyLog.Default.WriteLineAndConsole(ModName + $"Registered {weaponSubtypeIDs.Count} weapon block types");
                }
            }
        }

        //Dump current signals when hopping out of a grid
        private void GridChange(VRage.Game.ModAPI.Interfaces.IMyControllableEntity previousEnt, VRage.Game.ModAPI.Interfaces.IMyControllableEntity newEnt)
        {
            if (newEnt is IMyCharacter)
            {
                SignalList.Clear();
            }
        }

        public override void UpdateBeforeSimulation()
        {
            //Register client action of changing entity
            if (Client && !clientActionRegistered && Session?.Player?.Controller != null)
            {
                clientActionRegistered = true;
                Session.Player.Controller.ControlledEntityChanged += GridChange;
                MyLog.Default.WriteLineAndConsole(ModName + "Registered client ControlledEntityChanged action");
            }

            //Calc draw ratio figures based on resolution
            if (Client && symbolHeight == 0)
            {
                aspectRatio = Session.Camera.ViewportSize.X / Session.Camera.ViewportSize.Y;
                symbolHeight = Settings.Instance.symbolWidth * aspectRatio;
                offscreenHeight = Settings.Instance.offscreenWidth * aspectRatio;
            }

            //Time keeps on ticking
            Tick++;

            //Server timed updates
            #region ServerUpdates
            if (Server && Tick % 59 == 0)
            {
                //Init action to capture existing grids/blocks
                if ((!_startBlocks.IsEmpty || !_startGrids.IsEmpty))
                    StartComps();

                //Find player controlled entities in range and broadcast to them
                PlayerList.Clear();
                if (MPActive)
                    MyAPIGateway.Multiplayer.Players.GetPlayers(PlayerList);
                else
                    PlayerList.Add(Session.Player); //SP workaround
            }
            #endregion

            //Server main loop
            #region ServerLoop
            if (Server)
            {
                //Update grid comps to recalc signals on a background thread.  Rand element to make blipping the gas to avoid detection harder
                foreach (var gridComp in GridList)
                {
                    //Recalc a grid on a rolling random frequency with a max age of 59 ticks
                    //Using 236 in the rand to give an approx 1 in 4 chance of an early update, but no faster than every 15 ticks
                    if ((Tick - gridComp.lastUpdate - 15 > rand.Next(236) || gridComp.specialsDirty || Tick - gridComp.lastUpdate > 59) && !(((uint)gridComp.Grid.Flags & 0x20000000) > 0))
                        MyAPIGateway.Parallel.StartBackground(gridComp.CalcSignal);
                }

                //Update players if the last 2 digits of their identity ID = tick % 100 to spread out network updates.  If 100 ticks is too long, div by 2
                var tickMod = Tick % 100;
                foreach (var player in PlayerList)
                {
                    if (player == null || player.IsBot || player.Character == null || (MPActive && player.SteamUserId == 0) || (player.IdentityId % 100 != tickMod) || (!ServerSettings.Instance.SendSignalDataToSuits && player.Controller.ControlledEntity is IMyCharacter))
                    {
                        continue;
                    }

                    var playerPos = player.Character.WorldAABB.Center;
                    if (playerPos == Vector3D.Zero)
                    {
                        MyLog.Default.WriteLineAndConsole(ModName + $"Player position error - Vector3D.Zero - player.Name: {player.DisplayName} - player.SteamUserId: {player.SteamUserId}");
                        continue;
                    }
                    aPlayerQty++;
                    //Pull modifiers for current players grid (IE if it has increased detection range)
                    var controlledEnt = player.Controller?.ControlledEntity?.Entity?.Parent?.EntityId;
                    var block = player.Controller?.ControlledEntity?.Entity as IMyCubeBlock;
                    GridComp playerComp = null;
                    var playerGridDetectionModSqr = 0f;
                    if (block != null && GridListSpecials.TryGetValue(block.CubeGrid, out playerComp))
                    {
                        playerGridDetectionModSqr = playerComp.detectionRange * playerComp.detectionRange;
                        if (playerComp.detectionRange < 0)
                            playerGridDetectionModSqr *= -1;
                    }

                    var playerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId);
                    var validSignalList = new List<SignalComp>();

                    //For each player, iterate each grid
                    foreach (var grid in GridList)
                    {
                        var stealth = ((uint)grid.Grid.Flags & 0x20000000) > 0; //Stealth flag from Ash's mod
                        //TODO Skip concealed grids? any other conditions to skip a grid?
                        var playerGrid = grid.Grid.EntityId == controlledEnt;
                        if ((!playerGrid && grid.broadcastDist < 2) || stealth) continue;
                        var gridPos = grid.Grid.PositionComp.WorldAABB.Center;
                        var distToTargSqr = Vector3D.DistanceSquared(playerPos, gridPos);

                        //Check if current grid is in detection range of the player
                        if (playerGrid || distToTargSqr <= grid.broadcastDistSqr + playerGridDetectionModSqr)
                        {
                            var signalData = new SignalComp();
                            signalData.position = (Vector3I)gridPos;
                            signalData.range = playerGrid ? grid.broadcastDist : (int)Math.Sqrt(distToTargSqr);
                            signalData.faction = grid.faction;
                            signalData.entityID = grid.Grid.EntityId;
                            signalData.sizeEnum = grid.sizeEnum;
                            if (!playerGrid && playerFaction != null)
                            {
                                var relation = MyAPIGateway.Session.Factions.GetRelationBetweenFactions(playerFaction.FactionId, grid.factionID);
                                signalData.relation = (byte)relation;
                            }
                            else
                                signalData.relation = 0;
                            validSignalList.Add(signalData);
                        }
                    }
                    //If there's anything to send to the player, fire it off via the Networking or call the packet received method for SP
                    if(validSignalList.Count>0)
                    {
                        var packet = new PacketBase(validSignalList);
                        if (MPActive)
                        {
                            Networking.SendToPlayer(packet, player.SteamUserId);
                            aPacketQty++;
                        }
                        else
                            packet.Received();
                    }
                }

                //Analytics logging
                if(Tick > aLastLog + aLogTime)
                {
                    MyLog.Default.WriteLineAndConsole(ModName + $"Analytics:\n" +
                        $"GridComp CalcSignal calls - {aUpdateQty} - avg ticks between updates - {aUpdateTime / aUpdateQty}\n" +
                        $"Player iterations - {aPlayerQty} - Packets sent - {aPacketQty}");

                    aPacketQty = 0;
                    aPlayerQty = 0;
                    aUpdateQty = 0;
                    aUpdateTime = 0;

                    aLastLog = Tick;
                }
            }
            #endregion

            //Shutdown list updates in 5 tick interval to keep players from spamming keys to turn power back on.  Alternative is to register actions when the grid is in the shut down list, then de-register when removed.
            if (Server && Tick % 5 == 0 && powershutdownList.Count > 0)
            {
                foreach (var gridComp in powershutdownList.ToArray())
                    gridComp.TogglePower();
            }
            if (Server && Tick % 5 == 0 && thrustshutdownList.Count > 0)
            {
                foreach (var gridComp in thrustshutdownList.ToArray())
                    gridComp.ToggleThrust();
            }

        }

        //Main clientside loop for visuals
        public override void Draw()
        {
            if (Client && hudAPI.Heartbeat && SignalList.Count > 0 && MyAPIGateway.Session.Config.HudState != 0 && !MyAPIGateway.Gui.IsCursorVisible)
            {
                var s = Settings.Instance;
                var viewProjectionMat = Session.Camera.ViewMatrix * Session.Camera.ProjectionMatrix;
                var camPos = Session.Camera.Position;
                var playerEnt = MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity?.Parent?.EntityId;

                //Draw main signal list
                foreach (var signal in SignalList.ToArray())
                {
                    var contact = signal.Value.Item1;

                    //Signal for own occupied grid
                    if (contact.entityID == playerEnt)
                    {
                        var dispRange = contact.range > 1000 ? (contact.range / 1000f).ToString("0.#") + " km" : contact.range + " m";
                        var info = new StringBuilder("Broadcast Dist: " + dispRange + "\n" + "Size: " + messageList[contact.sizeEnum]);
                        var Label = new HudAPIv2.HUDMessage(info, s.signalDrawCoords, null, 2, s.textSizeOwn, true, true);
                        Label.Visible = true;
                    }
                    //Other grid signals received from server
                    else
                    {
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

                        var adjustedPos = camPos + Vector3D.Normalize((Vector3D)contact.position - camPos) * viewDist;
                        var screenCoords = Vector3D.Transform(adjustedPos, viewProjectionMat);
                        var offScreen = screenCoords.X > 1 || screenCoords.X < -1 || screenCoords.Y > 1 || screenCoords.Y < -1 || screenCoords.Z > 1;

                        //Draw symbol on grid with text
                        if (!offScreen) 
                        {
                            var symbolPosition = new Vector2D(screenCoords.X, screenCoords.Y);
                            var labelPosition = new Vector2D(screenCoords.X + (s.symbolWidth * 0.25), screenCoords.Y + (symbolHeight * 0.4));
                            var dispRange = distance > 1000 ? (distance / 1000).ToString("0.#") + " km" : distance.ToString("0.#") + " m";
                            //var dispSize = contact.range > 1000 ? (contact.range / 1000).ToString("0.#") + " km" : contact.range.ToString("0.#") + " m";
                            //var info = new StringBuilder(contact.faction + " " + dispSize + " sig " + "\n" + dispRange); //Testing alternate display
                            var info = new StringBuilder(contact.faction + messageList[contact.sizeEnum] + "\n" + dispRange);
                            var Label = new HudAPIv2.HUDMessage(info, labelPosition, new Vector2D(0, -0.001), 2, s.textSize, true, true);
                            Label.InitialColor = adjColor;
                            Label.Visible = true;
                            var symbolObj = new HudAPIv2.BillBoardHUDMessage(symbolList[contact.sizeEnum], symbolPosition, adjColor, Width: s.symbolWidth, Height: symbolHeight, TimeToLive: 2, HideHud: true, Shadowing: true);
                        }
                        //Draw off screen indicators and arrows
                        else
                        {
                            if (screenCoords.Z > 1)//Camera is between player and target
                                screenCoords *= -1;
                            var vectorToPt = new Vector2D(screenCoords.X, screenCoords.Y);
                            vectorToPt.Normalize();
                            vectorToPt *= offscreenSquish; //This flattens the Y axis so symbols don't overlap the hotbar and brings the X closer in
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
                if (dsAPI != null)
                    dsAPI.Unload();
            }
            if(Client)
            {
                Save(Settings.Instance);                
                if(clientActionRegistered)
                    Session.Player.Controller.ControlledEntityChanged -= GridChange;
            }
            if (wcAPI != null)
                wcAPI.Unload();
            if (hudAPI != null)
                hudAPI.Unload();
            Networking?.Unregister();
            Networking = null;
            PlayerList.Clear();
            SignalList.Clear();
            powershutdownList.Clear();
            thrustshutdownList.Clear();
        }
    }
}
