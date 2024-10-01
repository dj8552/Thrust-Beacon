using Digi.Example_NetworkProtobuf;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Utils;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage;

namespace ThrustBeacon
{
    public partial class Session : MySessionComponentBase
    {
        private void ClientTasks()
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

            //Clientside list processing to deconflict items shown by WC Radar
            if (Settings.Instance.hideWC && Tick % 119 == 0)
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
    }
}
