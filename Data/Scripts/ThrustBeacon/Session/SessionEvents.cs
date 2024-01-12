using Digi.Example_NetworkProtobuf;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;


namespace ThrustBeacon
{
    public partial class Session : MySessionComponentBase
    {
        //Send newly connected clients server-specific data (label text)
        private void PlayerConnected(long playerId)
        {
            MyLog.Default.WriteLineAndConsole($"{ModName}: Player connected " + playerId);
            var steamId = MyAPIGateway.Multiplayer.Players.TryGetSteamId(playerId);
            if (steamId != 0)
            {
                Networking.SendToPlayer(new PacketSettings(messageList), steamId);
                MyLog.Default.WriteLineAndConsole($"{ModName}: Sent settings to player " + steamId);
            }
            else
                MyLog.Default.WriteLineAndConsole($"{ModName}: Failed to find steam ID for playerId " + playerId);
        }

        //Dump current signals when hopping out of a grid
        private void GridChange(VRage.Game.ModAPI.Interfaces.IMyControllableEntity previousEnt, VRage.Game.ModAPI.Interfaces.IMyControllableEntity newEnt)
        {
            if (newEnt is IMyCharacter)
            {
                SignalList.Clear();
            }
        }
        private void GridGroupsOnOnGridGroupCreated(IMyGridGroupData group)
        {
            if (group.LinkType != GridLinkTypeEnum.Mechanical)
                return;
            GroupComp gComp = new GroupComp();
            gComp.iMyGroup = group;
            GroupDict.Add(group, gComp);
            gComp.InitGrids();

            group.OnGridAdded += gComp.OnGridAdded;
            group.OnGridRemoved += gComp.OnGridRemoved;
        }
        private void GridGroupsOnOnGridGroupDestroyed(IMyGridGroupData group)
        {
            if (group.LinkType != GridLinkTypeEnum.Mechanical)
                return;
            GroupComp gComp;
            if(GroupDict.TryGetValue(group, out gComp))
            {
                group.OnGridAdded -= gComp.OnGridAdded;
                group.OnGridRemoved -= gComp.OnGridRemoved;
                gComp.Clean();
                GroupDict.Remove(group);
            }
        }
    }
}
