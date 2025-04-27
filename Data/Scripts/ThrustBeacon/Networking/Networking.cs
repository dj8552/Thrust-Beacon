using Sandbox.ModAPI;

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
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(ChannelId, ReceivedPacket);
        }

        public void Unregister()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(ChannelId, ReceivedPacket);
        }

        private void ReceivedPacket(ushort handlerID, byte[] rawData, ulong ID, bool server)
        {
            if (!server)
                return;
            var packet = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase>(rawData);
            packet.Received();
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