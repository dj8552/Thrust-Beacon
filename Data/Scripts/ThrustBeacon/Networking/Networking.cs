using System;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Utils;

namespace Digi.Example_NetworkProtobuf
{
    public class Networking
    {
        public readonly ushort ChannelId;
        public Networking(ushort channelId)
        {
            ChannelId = channelId;
        }
        public void Register()
        {
            MyAPIGateway.Multiplayer.RegisterMessageHandler(ChannelId, ReceivedPacket);
        }
        public void Unregister()
        {
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(ChannelId, ReceivedPacket);
        }

        private void ReceivedPacket(byte[] rawData) // executed when a packet is received on this machine
        {
            try
            {
                var packet = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(rawData);
                packet.Received();
            }
            catch (Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");
                if (MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author]", 10000, MyFontEnum.Red);
            }
        }

        public void SendToPlayer(PacketBase packet, ulong steamId)
        {
            if (!MyAPIGateway.Multiplayer.IsServer)
                return;
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
            MyAPIGateway.Multiplayer.SendMessageTo(ChannelId, bytes, steamId);
        }
        public void SendToServer(PacketBase packet)
        {
            if (MyAPIGateway.Multiplayer.IsServer)
            {
                packet.Received();
                return;
            }
            var bytes = MyAPIGateway.Utilities.SerializeToBinary(packet);
            MyAPIGateway.Multiplayer.SendMessageToServer(ChannelId, bytes);
        }
    }
}