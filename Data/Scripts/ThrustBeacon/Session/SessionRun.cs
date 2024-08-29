using CoreSystems.Api;
using Digi.Example_NetworkProtobuf;
using Draygo.API;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Utils;
using VRageMath;
using VRage.Game.ModAPI;
using DefenseShields;
using Sandbox.Game;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage;
using VRage.ModAPI;

namespace ThrustBeacon
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public partial class Session : MySessionComponentBase
    {
        public override void BeforeStart()
        {
            Networking.Register();
            if (Server)
            {
                //Register group actions and init existing groups
                MyAPIGateway.GridGroups.OnGridGroupCreated += GridGroupsOnOnGridGroupCreated;
                MyAPIGateway.GridGroups.OnGridGroupDestroyed += GridGroupsOnOnGridGroupDestroyed;
                MyAPIGateway.Entities.GetEntities(entityHash, CheckPlanets);
                entityHash.Clear();
                var groupStartList = new List<IMyGridGroupData>();
                MyAPIGateway.GridGroups.GetGridGroups(GridLinkTypeEnum.Mechanical, groupStartList);
                foreach(var group in groupStartList)
                    GridGroupsOnOnGridGroupCreated(group);

                //Load NPC faction list
                Session.Factions.FactionCreated += FactionCreated;
                foreach(var faction in Session.Factions.Factions)
                    FactionCreated(faction.Key);
                //Hook connection event to send label list
                MyVisualScriptLogicProvider.PlayerConnected += PlayerConnected;
            }
            if(Client)
            {
                MyAPIGateway.Utilities.MessageEnteredSender += OnMessageEnteredSender;
            }

            //Init WC and register all defs on callback
            wcAPI = new WcApi();
            wcAPI.Load(RegisterWCDefs, true);
        }

        private bool CheckPlanets(IMyEntity entity)
        {
            if(entity is MyPlanet)
            {
                var planet = (MyPlanet)entity;
                
                var sphere = new BoundingSphereD(planet.PositionComp.WorldVolume.Center, planet.AverageRadius + planet.AtmosphereAltitude);
                planetSpheres.Add(sphere);
                MyLog.Default.WriteLine($"{ModName} Planet added {planet.Name} {planet.MinimumRadius} {planet.AverageRadius} {planet.MaximumRadius} {planet.AtmosphereRadius} {planet.AtmosphereAltitude}");
            }
            return false;
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
            if (Server)
            {
                dsAPI = new ShieldApi();
                dsAPI.Load();
                LoadSignalProducerConfigs(); //Blocks that generate signal (thrust, power)
                LoadBlockConfigs(); //Blocks that alter targeting
                InitServerConfig(); //Overall settings
                if (MyAPIGateway.Utilities.IsDedicated)
                    InitDefaults();
            }
        }

        public override void UpdateBeforeSimulation()
        {
            if (Client)
            {
                //Register client action of changing entity
                if (!clientActionRegistered && Session?.Player?.Controller != null)
                {
                    clientActionRegistered = true;
                    Session.Player.Controller.ControlledEntityChanged += GridChange;
                    GridChange(null, Session.Player.Controller.ControlledEntity);
                    MyLog.Default.WriteLineAndConsole(ModName + "Registered client ControlledEntityChanged action");
                }

                //Calc draw ratio figures based on resolution
                if (symbolHeight == 0)
                {
                    aspectRatio = Session.Camera.ViewportSize.X / Session.Camera.ViewportSize.Y;
                    symbolHeight = Settings.Instance.symbolWidth * aspectRatio;
                    offscreenHeight = Settings.Instance.offscreenWidth * aspectRatio;
                }

                //If first load of this mod, send default settings request to server
                if (firstLoad)
                {
                    Networking.SendToServer(new PacketRequestSettings(MyAPIGateway.Multiplayer.MyId));
                    firstLoad = false;
                    MyLog.Default.WriteLineAndConsole($"{ModName}: Requested default settings from server");
                }
            }

            //Time keeps on ticking
            Tick++;

            //Server timed updates
            #region ServerUpdates
            if (Server && Tick % 300 == 0)
            {
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
                var ss = ServerSettings.Instance;
                //Update grid comps to recalc signals on a background thread.  Rand element to make blipping the gas to avoid detection harder
                foreach (var group in GroupDict.Values)
                {
                    //Skip grid comps without fat blocks
                    if (group.groupFuncCount == 0)
                        continue;
                    //Recalc a grid on a rolling random frequency with a max age of 59 ticks
                    //Using 236 in the rand to give an approx 1 in 4 chance of an early update, but no faster than every 15 ticks
                    if (Tick - group.groupLastUpdate - 15 > rand.Next(236) || group.groupSpecialsDirty || Tick - group.groupLastUpdate > 59)
                        MyAPIGateway.Parallel.StartBackground(group.UpdateGroup);
                }

                //Send requested logs
                if (ReadyLogs.Count > 0)
                {
                    foreach (var readyLog in ReadyLogs)
                    {
                        Networking.SendToPlayer(new PacketStatsSend(readyLog.Key), readyLog.Value);
                    }
                    ReadyLogs.Clear();
                }

                //Update players if the last 2 digits of their identity ID = tick % 100 to spread out network updates.  If 100 ticks is too long, div by 2
                var tickMod = Tick % 100;
                foreach (var player in PlayerList)
                {
                    if (player == null || player.IsBot || player.Character == null || (MPActive && player.SteamUserId == 0) || (player.IdentityId % 100 != tickMod) || (!ServerSettings.Instance.SendSignalDataToSuits && player.Controller?.ControlledEntity is IMyCharacter))
                    {
                        continue;
                    }

                    var playerPos = player.Character.WorldAABB.Center;
                    if (playerPos == Vector3D.Zero)
                    {
                        MyLog.Default.WriteLineAndConsole(ModName + $"Player position error - Vector3D.Zero - player.Name: {player.DisplayName} - player.SteamUserId: {player.SteamUserId}");
                        continue;
                    }

                    //Pull modifiers for current players grid (IE if it has increased detection range)
                    var controlledGrid = (IMyCubeGrid)player.Controller?.ControlledEntity?.Entity?.Parent;
                    var playerGridDetectionModSqr = 0f;
                    var playerGridDetailMod = 0f;
                    if (controlledGrid != null)
                    {
                        GroupComp playerComp;
                        if (GroupDict.TryGetValue(controlledGrid.GetGridGroup(GridLinkTypeEnum.Mechanical), out playerComp))
                        { 
                            //var playerComp = GroupDict[controlledGrid.GetGridGroup(GridLinkTypeEnum.Mechanical)];
                            playerGridDetectionModSqr = playerComp.groupDetectionRange * playerComp.groupDetectionRange;
                            if (playerComp.groupDetectionRange < 0)
                                playerGridDetectionModSqr *= -1;
                            playerGridDetailMod = playerComp.groupDetailMod;
                        }
                    }

                    var playerFaction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(player.IdentityId);
                    var validSignalList = new List<SignalComp>();

                    //For each player, iterate each grid
                    foreach (var group in GroupDict.Values)
                    {
                        var stealth = false;//((uint)grid.Grid.Flags & 0x20000000) > 0; //Stealth flag from Ash's mod
                        var playerGrid = controlledGrid == null ?  false : group.GridDict.ContainsKey(controlledGrid);
                        if ((!playerGrid && group.groupBroadcastDist < 2) || stealth || group.groupFuncCount == 0) continue;
                        var gridPos = group.groupSphere.Center;
                        var distToTargSqr = Vector3D.DistanceSquared(playerPos, gridPos);
                        if (!playerGrid && distToTargSqr > group.groupBroadcastDistSqr + playerGridDetectionModSqr) continue; //Distance check

                        var planetOcclusion = false;
                        if (!playerGrid)
                        {
                            var dirRay = new RayD(playerPos, Vector3D.Normalize(gridPos - playerPos));
                            foreach (var planet in planetSpheres)
                            {
                                var hitDist = dirRay.Intersects(planet);
                                if (hitDist != null && hitDist * hitDist < distToTargSqr)
                                {
                                    var onPlanet = planet.Contains(gridPos) == ContainmentType.Contains;
                                    if (!onPlanet || (onPlanet && Vector3D.DistanceSquared(planet.Center, gridPos) <= distToTargSqr))
                                    {
                                        planetOcclusion = true;
                                        break;
                                    }
                                }
                            }
                        }
                        if (planetOcclusion) continue;

                        var masked = ss.EnableDataMasking && distToTargSqr > group.groupBroadcastDistSqr * (ss.DataMaskingRange + playerGridDetailMod) * (ss.DataMaskingRange + playerGridDetailMod);
                        var sameFaction = playerFaction != null && playerFaction.FactionId == group.groupFactionID;
                        var signalData = new SignalComp();
                        signalData.position = (Vector3I)gridPos;
                        signalData.range = playerGrid ? group.groupBroadcastDist : (int)Math.Sqrt(distToTargSqr);
                        signalData.faction = masked && !sameFaction ? "" : group.groupFaction;
                        signalData.entityID = playerGrid ? controlledGrid.EntityId : group.GridDict.FirstPair().Key.EntityId;
                        signalData.sizeEnum = group.groupSizeEnum;
                        if (playerGrid) //Own grid
                            signalData.relation = 4;
                        else if (!masked && !playerGrid && playerFaction != null && !sameFaction)//Not in player faction
                            signalData.relation = (byte)MyAPIGateway.Session.Factions.GetRelationBetweenFactions(playerFaction.FactionId, group.groupFactionID);
                        else if (playerFaction != null && sameFaction)//In player faction
                            signalData.relation = 3;
                        else if (!masked)//Factionless, presumed hostile
                            signalData.relation = 1;
                        else if (masked)//Outside detail range, mask data
                            signalData.relation = 2;
                        validSignalList.Add(signalData);
                    }
                    //If there's anything to send to the player, fire it off via the Networking or call the packet received method for SP
                    if(validSignalList.Count>0)
                    {
                        var packet = new PacketSignals(validSignalList);
                        if (MPActive)
                            Networking.SendToPlayer(packet, player.SteamUserId);
                        else
                            packet.Received();
                    }
                }
            }
            #endregion

            //Shutdown list updates in 5 tick interval to keep players from spamming keys to turn power back on.
            //Alternative is to register actions when the grid is in the shut down list, then de-register when removed.
            if (Server && Tick % 5 == 0 && powershutdownList.Count > 0)
            {
                foreach (var groupComp in powershutdownList.ToArray())
                    groupComp.TogglePower();
            }
            if (Server && Tick % 5 == 0 && thrustshutdownList.Count > 0)
            {
                foreach (var groupComp in thrustshutdownList.ToArray())
                    groupComp.ToggleThrust();
            }

            //Clientside list processing to deconflict items shown by WC Radar
            if (Client && Settings.Instance.hideWC && Tick % 119 == 0)
            {
                var threatList = new List<MyTuple<MyEntity, float>>();
                var obsList = new List<MyEntity>();
                entityIDList.Clear();
                var controlledEnt = MyAPIGateway.Session?.Player?.Controller?.ControlledEntity?.Entity?.Parent;
                if (controlledEnt != null && controlledEnt is MyCubeGrid)
                {
                    //Scrape WC Data to one list of entities
                    var myEnt = (MyEntity)controlledEnt;
                    wcAPI.GetSortedThreats(myEnt, threatList);
                    foreach (var item in threatList)
                        entityIDList.Add(item.Item1.EntityId);
                    wcAPI.GetObstructions(myEnt, obsList);
                    foreach (var item in obsList)
                        entityIDList.Add(item.EntityId);
                }
            }
        }

        protected override void UnloadData()
        {
            if (Server)
            {
                Clean();
                if (dsAPI != null)
                    dsAPI.Unload();
                try //Because this throws a NRE in keen code if you alt-F4
                {
                    MyAPIGateway.GridGroups.OnGridGroupCreated -= GridGroupsOnOnGridGroupCreated;
                    MyAPIGateway.GridGroups.OnGridGroupDestroyed -= GridGroupsOnOnGridGroupDestroyed;
                }
                catch { }
                MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
                Session.Factions.FactionCreated -= FactionCreated;
            }
            if (Client)
            {
                Save(Settings.Instance);
                if (clientActionRegistered)
                {
                    clientActionRegistered = false;
                    Session.Player.Controller.ControlledEntityChanged -= GridChange;
                }
                if (primaryBeacon != null)
                    Beacon_OnClosing(primaryBeacon);
                MyAPIGateway.Utilities.MessageEnteredSender -= OnMessageEnteredSender;
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
