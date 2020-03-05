using System.Net;
using System.Text;
using System.Threading;
using Arrowgene.Enet.Log;

namespace Arrowgene.ENet.Playground
{
    public class ENetServer
    {
        private ENetHost _eNetHost;
        private bool _running;
        private ILogger _logger;
        private Thread _thread;

        public ENetServer()
        {
            _running = false;
            _logger = LogProvider.Logger(GetType());
            _eNetHost = new ENetHost(new IPEndPoint(IPAddress.Loopback, 12345), 32, 2, 0, 0);
        }

        public void Start()
        {
            _running = true;
            _thread = new Thread(Run);
            _thread.Name = "ENet";
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
        }

        private void Run()
        {
            ENetEvent eNetEvent = new ENetEvent();
            while (_running)
            {
                int result = _eNetHost.Service(eNetEvent, 1000);
                _logger.Info($"Result: {result}");
                switch (eNetEvent.Type)
                {
                    case ENetEventType.ENET_EVENT_TYPE_CONNECT:
                        _logger.Info(
                            $"A new client connected from: {eNetEvent.Peer.Address.Address}:{eNetEvent.Peer.Address.Port}");
                        break;
                    case ENetEventType.ENET_EVENT_TYPE_RECEIVE:
                        _logger.Info(
                            $"A packet of length {eNetEvent.Packet.DataLength} was received from {eNetEvent.Peer.Address.Address}:{eNetEvent.Peer.Address.Port} on channel {eNetEvent.ChannelId}");
                        ENetPacket packet = new ENetPacket();
                        packet.Data = Encoding.UTF8.GetBytes("Test Response\0");
                        packet.DataLength = (uint)packet.Data.Length;
                        packet.Flags = ENetPacketFlags.ENET_PACKET_FLAG_RELIABLE;
                        eNetEvent.Peer.Send(eNetEvent.ChannelId, packet);
                        break;
                    case ENetEventType.ENET_EVENT_TYPE_DISCONNECT:
                        _logger.Info(
                            $"Client {eNetEvent.Peer.Address.Address}:{eNetEvent.Peer.Address.Port} disconnected");
                        break;
                }
            }
        }
    }
}
