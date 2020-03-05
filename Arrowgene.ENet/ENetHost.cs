using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using Arrowgene.ENet.Common;
using Arrowgene.Enet.Log;
using Arrowgene.ENet.Protocol;

// ReSharper disable All

namespace Arrowgene.ENet
{
    public class ENetHost
    {
        public const uint ENET_BUFFER_MAXIMUM = 1 + 2 * ENET_PROTOCOL_MAXIMUM_PACKET_COMMANDS;

        public const uint ENET_HOST_ANY = 0;
        public const uint ENET_HOST_BROADCAST = 0xFFFFFFFFU;
        public const uint ENET_HOST_RECEIVE_BUFFER_SIZE = 256 * 1024;
        public const uint ENET_HOST_SEND_BUFFER_SIZE = 256 * 1024;
        public const uint ENET_HOST_BANDWIDTH_THROTTLE_INTERVAL = 1000;
        public const uint ENET_HOST_DEFAULT_MTU = 1400;
        public const uint ENET_HOST_DEFAULT_MAXIMUM_PACKET_SIZE = 32 * 1024 * 1024;
        public const uint ENET_HOST_DEFAULT_MAXIMUM_WAITING_DATA = 32 * 1024 * 1024;

        public const uint ENET_PROTOCOL_MINIMUM_MTU = 576;
        public const uint ENET_PROTOCOL_MAXIMUM_MTU = 4096;
        public const uint ENET_PROTOCOL_MAXIMUM_PACKET_COMMANDS = 32;
        public const uint ENET_PROTOCOL_MINIMUM_WINDOW_SIZE = 4096;
        public const uint ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE = 65536;
        public const uint ENET_PROTOCOL_MINIMUM_CHANNEL_COUNT = 1;
        public const uint ENET_PROTOCOL_MAXIMUM_CHANNEL_COUNT = 255;
        public const ushort ENET_PROTOCOL_MAXIMUM_PEER_ID = 0xFFF;
        public const uint ENET_PROTOCOL_MAXIMUM_FRAGMENT_COUNT = 1024 * 1024;

        private static int[] CommandSizes = new int[(int) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_COUNT]
        {
            0,
            ENetProtocolAcknowledge.Size,
            ENetProtocolConnect.Size,
            ENetProtocolVerifyConnect.Size,
            ENetProtocolDisconnect.Size,
            ENetProtocolPing.Size,
            ENetProtocolSendReliable.Size,
            ENetProtocolSendUnreliable.Size,
            ENetProtocolSendFragment.Size,
            ENetProtocolSendUnsequenced.Size,
            ENetProtocolBandwidthLimit.Size,
            ENetProtocolThrottleConfigure.Size,
            ENetProtocolSendFragment.Size
        };

        private Socket _socket;
        private IPEndPoint _address;
        private uint _incomingBandwidth;
        private uint _outgoingBandwidth;
        private uint _bandwidthThrottleEpoch;
        private uint _mtu;
        private uint _randomSeed;
        private bool _recalculateBandwidthLimits;
        private List<ENetPeer> _peers;
        private uint _peerCount;
        private uint _channelLimit;
        private uint _serviceTime;
        private List<ENetPeer> _dispatchQueue;
        private bool _continueSending;
        private uint _packetSize;
        private ENetProtocolFlag _headerFlags;

        /**
         * pool of commands to reuse
         */
        private ENetProtocol[] _commands;

        /**
         * number of used commands
         */
        private uint _commandCount;

        private ENetBuffer[] _buffers;
        private uint _bufferCount;
        public IENetChecksumCallback _checksum { get; private set; }
        private IENetCompressor _compressor;
        private byte[][] _packetData;
        private IPEndPoint _receivedAddress;
        private byte[] _receivedData;
        private uint _receivedDataLength;
        private uint _totalSentData;
        private uint _totalSentPackets;
        private uint _totalReceivedData;
        private uint _totalReceivedPackets;
        private IENetInterceptCallback _intercept;
        private uint _connectedPeers;
        private uint _bandwidthLimitedPeers;

        /**
         * optional number of allowed peers from duplicate IPs, defaults to ENET_PROTOCOL_MAXIMUM_PEER_ID
         */
        private uint _duplicatePeers;

        public uint _maximumPacketSize { get; private set; }
        private uint _maximumWaitingData;


        // Additional
        private ENetTime _time;
        private ILogger _logger;
        private uint _receivedDataOffset;

        public ENetHost(IPEndPoint address, uint peerCount, uint channelLimit, uint incomingBandwidth,
            uint outgoingBandwidth)
        {
            _logger = LogProvider.Logger(GetType());

            if (peerCount > ENET_PROTOCOL_MAXIMUM_PEER_ID)
            {
                throw new Exception($"PeerCount: {peerCount} larger than maximum: {ENET_PROTOCOL_MAXIMUM_PEER_ID}");
            }

            _peers = new List<ENetPeer>((int) peerCount);

            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.Bind(address);
            _socket.Blocking = false;
            _socket.EnableBroadcast = true;
            _socket.SendBufferSize = (int) ENET_HOST_SEND_BUFFER_SIZE;
            _socket.ReceiveBufferSize = (int) ENET_HOST_RECEIVE_BUFFER_SIZE;


            if (channelLimit > ENET_PROTOCOL_MAXIMUM_CHANNEL_COUNT)
            {
                channelLimit = ENET_PROTOCOL_MAXIMUM_CHANNEL_COUNT;
            }
            else if (channelLimit < ENET_PROTOCOL_MINIMUM_CHANNEL_COUNT)
            {
                channelLimit = ENET_PROTOCOL_MINIMUM_CHANNEL_COUNT;
            }

            _randomSeed = RandomSeed();
            _randomSeed = _randomSeed << 16 | _randomSeed >> 16;
            _channelLimit = channelLimit;
            _incomingBandwidth = incomingBandwidth;
            _outgoingBandwidth = outgoingBandwidth;
            _bandwidthThrottleEpoch = 0;
            _recalculateBandwidthLimits = false;
            _mtu = ENET_HOST_DEFAULT_MTU;
            _peerCount = peerCount;
            _commands = new ENetProtocol[ENET_PROTOCOL_MAXIMUM_PACKET_COMMANDS];
            for (int i = 0; i < ENET_PROTOCOL_MAXIMUM_PACKET_COMMANDS; i++)
            {
                _commands[i] = new ENetProtocol();
            }

            _commandCount = 0;
            _buffers = new ENetBuffer[ENET_BUFFER_MAXIMUM];
            for (int i = 0; i < ENET_BUFFER_MAXIMUM; i++)
            {
                _buffers[i] = new ENetBuffer();
            }

            _bufferCount = 0;
            _checksum = null;
            _receivedAddress = new IPEndPoint(IPAddress.Any, 0);
            _receivedData = null;
            _receivedDataLength = 0;

            _totalSentData = 0;
            _totalSentPackets = 0;
            _totalReceivedData = 0;
            _totalReceivedPackets = 0;

            _connectedPeers = 0;
            _bandwidthLimitedPeers = 0;
            _duplicatePeers = ENET_PROTOCOL_MAXIMUM_PEER_ID;
            _maximumPacketSize = ENET_HOST_DEFAULT_MAXIMUM_PACKET_SIZE;
            _maximumWaitingData = ENET_HOST_DEFAULT_MAXIMUM_WAITING_DATA;

            _compressor = null;
            _intercept = null;

            _dispatchQueue = new List<ENetPeer>();


            _packetData = new byte[2][]
            {
                new byte[ENET_PROTOCOL_MAXIMUM_MTU],
                new byte[ENET_PROTOCOL_MAXIMUM_MTU]
            };
            _time = new ENetTime();
            for (ushort i = 0; i < _peerCount; i++)
            {
                ENetPeer peer = new ENetPeer();
                peer.Host = this;
                peer.IncomingPeerId = i;
                peer.OutgoingSessionId = 0xFF;
                peer.IncomingSessionId = 0xFF;
                peer.Data = null;
                peer.Reset();
                _peers.Add(peer);
            }
        }

        public uint Mtu => _mtu;


        /// <summary>
        /// > 0 if an event occurred within the specified time limit
        /// 0 if no event occurred
        /// < 0 on failure
        /// </summary>
        public int Service(ENetEvent eNetEvent, uint timeout)
        {
            ENetSocketWait waitCondition;
            if (eNetEvent != null)
            {
                eNetEvent.Type = ENetEventType.ENET_EVENT_TYPE_NONE;
                eNetEvent.Peer = null;
                eNetEvent.Packet = null;

                switch (enet_protocol_dispatch_incoming_commands(eNetEvent))
                {
                    case 1:
                        return 1;
                    case -1:
                        _logger.Error("Error dispatching incoming packets");
                        return -1;
                    default:
                        break;
                }
            }

            _serviceTime = _time.GetTime();
            timeout += _serviceTime;
            do
            {
                if (_time.Difference(_serviceTime, _bandwidthThrottleEpoch) >= ENET_HOST_BANDWIDTH_THROTTLE_INTERVAL)
                {
                    enet_host_bandwidth_throttle();
                }

                switch (enet_protocol_send_outgoing_commands(eNetEvent, true))
                {
                    case 1:
                        return 1;
                    case -1:
                        _logger.Error("Error sending outgoing packets");
                        return -1;
                    default:
                        break;
                }

                switch (enet_protocol_receive_incoming_commands(eNetEvent))
                {
                    case 1:
                        return 1;
                    case -1:
                        _logger.Error("Error receiving incoming packets");
                        return -1;
                    default:
                        break;
                }

                switch (enet_protocol_send_outgoing_commands(eNetEvent, true))
                {
                    case 1:
                        return 1;

                    case -1:
                        _logger.Error("Error sending outgoing packets");
                        return -1;
                    default:
                        break;
                }

                if (eNetEvent != null)
                {
                    switch (enet_protocol_dispatch_incoming_commands(eNetEvent))
                    {
                        case 1:
                            return 1;
                        case -1:
                            _logger.Error("Error dispatching incoming packets");
                            return -1;
                        default:
                            break;
                    }
                }

                if (_time.GreaterEqual(_serviceTime, timeout))
                {
                    return 0;
                }

                do
                {
                    _serviceTime = _time.GetTime();
                    if (_time.GreaterEqual(_serviceTime, timeout))
                    {
                        return 0;
                    }

                    waitCondition = ENetSocketWait.ENET_SOCKET_WAIT_RECEIVE | ENetSocketWait.ENET_SOCKET_WAIT_INTERRUPT;
                    if (enet_socket_wait(null, ref waitCondition, _time.Difference(timeout, _serviceTime)) != 0)
                    {
                        return -1;
                    }
                } while (waitCondition.HasFlag(ENetSocketWait.ENET_SOCKET_WAIT_INTERRUPT));

                _serviceTime = _time.GetTime();
            } while (waitCondition.HasFlag(ENetSocketWait.ENET_SOCKET_WAIT_RECEIVE));

            return 0;
        }

        private int enet_protocol_dispatch_incoming_commands(ENetEvent eNetEvent)
        {
            List<ENetPeer> dispatchQueue = new List<ENetPeer>(_dispatchQueue);
            foreach (ENetPeer peer in dispatchQueue)
            {
                _dispatchQueue.Remove(peer);
                peer.NeedsDispatch = false;

                switch (peer.State)
                {
                    case ENetPeerState.ENET_PEER_STATE_CONNECTION_PENDING:
                    case ENetPeerState.ENET_PEER_STATE_CONNECTION_SUCCEEDED:
                    {
                        enet_protocol_change_state(peer, ENetPeerState.ENET_PEER_STATE_CONNECTED);
                        eNetEvent.Type = ENetEventType.ENET_EVENT_TYPE_CONNECT;
                        eNetEvent.Peer = peer;
                        eNetEvent.Data = peer.EventData;
                        return 1;
                    }
                    case ENetPeerState.ENET_PEER_STATE_ZOMBIE:
                    {
                        _recalculateBandwidthLimits = true;
                        eNetEvent.Type = ENetEventType.ENET_EVENT_TYPE_DISCONNECT;
                        eNetEvent.Peer = peer;
                        eNetEvent.Data = peer.EventData;
                        enet_peer_reset(peer);
                        return 1;
                    }
                    case ENetPeerState.ENET_PEER_STATE_CONNECTED:
                    {
                        if (peer.DispatchedCommands.Count <= 0)
                        {
                            continue;
                        }

                        eNetEvent.Packet = enet_peer_receive(peer, eNetEvent.ChannelId);
                        if (eNetEvent.Packet == null)
                        {
                            continue;
                        }

                        eNetEvent.Type = ENetEventType.ENET_EVENT_TYPE_RECEIVE;
                        eNetEvent.Peer = peer;

                        if (peer.DispatchedCommands.Count > 0)
                        {
                            peer.NeedsDispatch = true;
                            _dispatchQueue.Add(peer);
                        }

                        return 1;
                    }
                    default:
                        break;
                }
            }

            return 0;
        }

        private ENetPacket enet_peer_receive(ENetPeer peer, byte channelId)
        {
            if (peer.DispatchedCommands.Count <= 0)
            {
                return null;
            }

            ENetIncomingCommand incomingCommand = peer.DispatchedCommands[0];
            peer.DispatchedCommands.Remove(incomingCommand);
            if (channelId != null)
            {
                channelId = incomingCommand.Command.Header.ChannelId;
            }

            ENetPacket packet = incomingCommand.Packet;
            --packet.ReferenceCount;
            if (incomingCommand.Fragments != null)
            {
                //enet_free (incomingCommand -> fragments);
                incomingCommand.Fragments = null;
            }

            // enet_free(incomingCommand);
            peer.TotalWaitingData -= packet.DataLength;
            return packet;
        }

        private void enet_host_bandwidth_throttle()
        {
            uint timeCurrent = _time.GetTime();
            uint elapsedTime = timeCurrent - _bandwidthThrottleEpoch;
            uint peersRemaining = _connectedPeers;
            uint dataTotal = uint.MaxValue;
            uint bandwidth = uint.MaxValue;
            uint throttle = 0;
            uint bandwidthLimit = 0;
            bool needsAdjustment = _bandwidthLimitedPeers > 0;

            if (elapsedTime < ENET_HOST_BANDWIDTH_THROTTLE_INTERVAL)
            {
                return;
            }

            _bandwidthThrottleEpoch = timeCurrent;

            if (peersRemaining == 0)
            {
                return;
            }

            if (_outgoingBandwidth != 0)
            {
                dataTotal = 0;
                bandwidth = (_outgoingBandwidth * elapsedTime) / 1000;

                foreach (ENetPeer peer in _peers)
                {
                    if (peer.State != ENetPeerState.ENET_PEER_STATE_CONNECTED &&
                        peer.State != ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER)
                    {
                        continue;
                    }

                    dataTotal += peer.OutgoingDataTotal;
                }
            }

            while (peersRemaining > 0 && needsAdjustment)
            {
                needsAdjustment = false;

                if (dataTotal <= bandwidth)
                    throttle = ENetPeer.ENET_PEER_PACKET_THROTTLE_SCALE;
                else
                    throttle = (bandwidth * ENetPeer.ENET_PEER_PACKET_THROTTLE_SCALE) / dataTotal;


                foreach (ENetPeer peer in _peers)
                {
                    uint peerBandwidth;

                    if ((peer.State != ENetPeerState.ENET_PEER_STATE_CONNECTED &&
                         peer.State != ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER) ||
                        peer.IncomingBandwidth == 0 ||
                        peer.OutgoingBandwidthThrottleEpoch == timeCurrent)
                        continue;

                    peerBandwidth = (peer.IncomingBandwidth * elapsedTime) / 1000;
                    if ((throttle * peer.OutgoingDataTotal) / ENetPeer.ENET_PEER_PACKET_THROTTLE_SCALE <= peerBandwidth)
                        continue;

                    peer.PacketThrottleLimit = (peerBandwidth *
                                                ENetPeer.ENET_PEER_PACKET_THROTTLE_SCALE) / peer.OutgoingDataTotal;

                    if (peer.PacketThrottleLimit == 0)
                        peer.PacketThrottleLimit = 1;

                    if (peer.PacketThrottle > peer.PacketThrottleLimit)
                        peer.PacketThrottle = peer.PacketThrottleLimit;

                    peer.OutgoingBandwidthThrottleEpoch = timeCurrent;

                    peer.IncomingDataTotal = 0;
                    peer.OutgoingDataTotal = 0;

                    needsAdjustment = true;
                    --peersRemaining;
                    bandwidth -= peerBandwidth;
                    dataTotal -= peerBandwidth;
                }
            }
        }

        private int enet_protocol_send_outgoing_commands(ENetEvent eNetEvent, bool checkForTimeouts)
        {
            _continueSending = true;
            while (_continueSending)
            {
                _continueSending = false;
                foreach (ENetPeer currentPeer in _peers)
                {
                    if (currentPeer.State == ENetPeerState.ENET_PEER_STATE_DISCONNECTED ||
                        currentPeer.State == ENetPeerState.ENET_PEER_STATE_ZOMBIE)
                    {
                        continue;
                    }

                    _headerFlags = 0;
                    _commandCount = 0;
                    _bufferCount = 1;
                    _packetSize = ENetProtocolHeader.Size;

                    if (currentPeer.Acknowledgements.Count > 0)
                    {
                        enet_protocol_send_acknowledgements(currentPeer);
                    }

                    if (checkForTimeouts &&
                        currentPeer.SentReliableCommands.Count > 0 &&
                        _time.GreaterEqual(_serviceTime, currentPeer.NextTimeout) &&
                        enet_protocol_check_timeouts(currentPeer, eNetEvent) == 1)
                    {
                        if (eNetEvent != null && eNetEvent.Type != ENetEventType.ENET_EVENT_TYPE_NONE)
                        {
                            return 1;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if ((currentPeer.OutgoingReliableCommands.Count <= 0 ||
                         enet_protocol_send_reliable_outgoing_commands(currentPeer) != 0) &&
                        currentPeer.SentReliableCommands.Count <= 0 &&
                        _time.Difference(_serviceTime, currentPeer.LastReceiveTime) >=
                        currentPeer.PingInterval &&
                        currentPeer.Mtu - _packetSize >= ENetProtocolPing.Size)
                    {
                        enet_peer_ping(currentPeer);
                        enet_protocol_send_reliable_outgoing_commands(currentPeer);
                    }

                    if (currentPeer.OutgoingUnreliableCommands.Count > 0)
                    {
                        enet_protocol_send_unreliable_outgoing_commands(currentPeer);
                    }

                    if (_commandCount == 0)
                    {
                        continue;
                    }

                    if (currentPeer.PacketLossEpoch == 0)
                    {
                        currentPeer.PacketLossEpoch = _serviceTime;
                    }

                    else if (_time.Difference(_serviceTime, currentPeer.PacketLossEpoch) >=
                             ENetPeer.ENET_PEER_PACKET_LOSS_INTERVAL &&
                             currentPeer.PacketsSent > 0)
                    {
                        uint packetLoss = currentPeer.PacketsLost * ENetPeer.ENET_PEER_PACKET_LOSS_SCALE /
                                          currentPeer.PacketsSent;
#if DEBUG
                        _logger.Debug(String.Format(
                            "peer {0}: {1:0.000000}%+-{2:0.000000}% packet loss, {3}+-{4} ms round trip time, {5:0.000000}% throttle, {6}/{7} outgoing, {8}/{9} incoming\n",
                            currentPeer.IncomingPeerId,
                            currentPeer.PacketLoss / (float) ENetPeer.ENET_PEER_PACKET_LOSS_SCALE,
                            currentPeer.PacketLossVariance / (float) ENetPeer.ENET_PEER_PACKET_LOSS_SCALE,
                            currentPeer.RoundTripTime,
                            currentPeer.RoundTripTimeVariance,
                            currentPeer.PacketThrottle / (float) ENetPeer.ENET_PEER_PACKET_THROTTLE_SCALE,
                            currentPeer.OutgoingReliableCommands.Count,
                            currentPeer.OutgoingUnreliableCommands.Count,
                            ENetList.enet_list_size(
                                currentPeer.Channels, channel => channel.IncomingReliableCommands.Count
                            ),
                            ENetList.enet_list_size(
                                currentPeer.Channels, channel => channel.IncomingUnreliableCommands.Count
                            )
                        ));
#endif

                        currentPeer.PacketLossVariance -= currentPeer.PacketLossVariance / 4;

                        if (packetLoss >= currentPeer.PacketLoss)
                        {
                            currentPeer.PacketLoss += (packetLoss - currentPeer.PacketLoss) / 8;
                            currentPeer.PacketLossVariance += (packetLoss - currentPeer.PacketLoss) / 4;
                        }
                        else
                        {
                            currentPeer.PacketLoss -= (currentPeer.PacketLoss - packetLoss) / 8;
                            currentPeer.PacketLossVariance += (currentPeer.PacketLoss - packetLoss) / 4;
                        }

                        currentPeer.PacketLossEpoch = _serviceTime;
                        currentPeer.PacketsSent = 0;
                        currentPeer.PacketsLost = 0;
                    }

                    ENetProtocolHeader header = new ENetProtocolHeader();
                    if (_headerFlags.HasFlag(ENetProtocolFlag.ENET_PROTOCOL_HEADER_FLAG_SENT_TIME))
                    {
                        header.SentTime = ENetSocket.ENET_HOST_TO_NET_16((ushort) _serviceTime);
                        _buffers[0].DataLength = ENetProtocolHeader.Size;
                    }
                    else
                    {
                        _buffers[0].DataLength = ENetProtocolHeader.SizeWithoutSentTime;
                    }

                    uint shouldCompress = 0;
                    if (_compressor != null)
                    {
                        uint originalSize = _packetSize - ENetProtocolHeader.Size;
                        uint compressedSize = _compressor.Compress(_buffers, 1, _bufferCount - 1,
                            originalSize, out _packetData[1], originalSize
                        );
                        if (compressedSize > 0 && compressedSize < originalSize)
                        {
                            _headerFlags |= ENetProtocolFlag.ENET_PROTOCOL_HEADER_FLAG_COMPRESSED;
                            shouldCompress = compressedSize;
#if DEBUG
                            _logger.Debug(String.Format("peer {0}: compressed {1} -> {2} ({3}%%)\n",
                                currentPeer.IncomingPeerId,
                                originalSize,
                                compressedSize,
                                (compressedSize * 100) / originalSize
                            ));
#endif
                        }
                    }

                    if (currentPeer.OutgoingPeerId < ENET_PROTOCOL_MAXIMUM_PEER_ID)
                    {
                        _headerFlags = (ENetProtocolFlag) ((int) _headerFlags |
                                                           (currentPeer.OutgoingSessionId <<
                                                            (int) ENetProtocolFlag.ENET_PROTOCOL_HEADER_SESSION_SHIFT));
                    }

                    header.PeerId =
                        ENetSocket.ENET_HOST_TO_NET_16((ushort) (currentPeer.OutgoingPeerId | (int) _headerFlags));

                    _buffers[0].Data = Serialize(header);

                    if (_checksum != null)
                    {
                        // TODO check this, ednianness and if OutgoingPeerId is used.
                        //  uint checksum = currentPeer.OutgoingPeerId < ENET_PROTOCOL_MAXIMUM_PEER_ID
                        //      ? currentPeer.ConnectId
                        //      : 0;
                        uint checksum = _checksum.Calculate(_buffers, 0, _bufferCount);
                        byte[] checksuArr = BitConverter.GetBytes(checksum);

                        byte[] tmp = new byte[(int) _buffers[0].DataLength + 4];
                        int i = 0;
                        for (; i < _buffers[0].DataLength; i++)
                        {
                            tmp[i] = _buffers[0].Data[i];
                        }

                        tmp[i++] = checksuArr[0];
                        tmp[i++] = checksuArr[1];
                        tmp[i++] = checksuArr[2];
                        tmp[i++] = checksuArr[3];
                        _buffers[0].Data = tmp;
                        _buffers[0].DataLength = (uint) tmp.Length;
                    }

                    if (shouldCompress > 0)
                    {
                        _buffers[1].Data = _packetData[1];
                        _buffers[1].DataLength = shouldCompress;
                        _bufferCount = 2;
                    }

                    currentPeer.LastSendTime = _serviceTime;


                    int sentLength = enet_socket_send(_socket, currentPeer.Address, _buffers, _bufferCount);

                    enet_protocol_remove_sent_unreliable_commands(currentPeer);

                    if (sentLength < 0)
                    {
                        return -1;
                    }

                    _totalSentData += (uint) sentLength;
                    _totalSentPackets++;
                }
            }

            return 0;
        }

        private void enet_protocol_remove_sent_unreliable_commands(ENetPeer peer)
        {
            ENetOutgoingCommand outgoingCommand;

            if (peer.SentUnreliableCommands.Count <= 0)
            {
                return;
            }

            peer.SentUnreliableCommands.Clear();

            if (peer.State == ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER &&
                peer.OutgoingReliableCommands.Count <= 0 &&
                peer.OutgoingUnreliableCommands.Count <= 0 &&
                peer.SentReliableCommands.Count <= 0)
            {
                enet_peer_disconnect(peer, peer.EventData);
            }
        }

        void enet_peer_disconnect(ENetPeer peer, uint data)
        {
            if (peer.State == ENetPeerState.ENET_PEER_STATE_DISCONNECTING ||
                peer.State == ENetPeerState.ENET_PEER_STATE_DISCONNECTED ||
                peer.State == ENetPeerState.ENET_PEER_STATE_ACKNOWLEDGING_DISCONNECT ||
                peer.State == ENetPeerState.ENET_PEER_STATE_ZOMBIE)
            {
                return;
            }

            peer.ResetQueues();

            ENetProtocol command = new ENetProtocol();

            command.Header.Command = (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_DISCONNECT;
            command.Header.ChannelId = 0xFF;
            command.Disconnect.Data = ENetSocket.ENET_HOST_TO_NET_32(data);

            if (peer.State == ENetPeerState.ENET_PEER_STATE_CONNECTED ||
                peer.State == ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER)
            {
                command.Header.Command |= (byte) ENetProtocolFlag.ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE;
            }
            else
            {
                command.Header.Command |= (byte) ENetProtocolFlag.ENET_PROTOCOL_COMMAND_FLAG_UNSEQUENCED;
            }


            enet_peer_queue_outgoing_command(peer, command, null, 0, 0);

            if (peer.State == ENetPeerState.ENET_PEER_STATE_CONNECTED ||
                peer.State == ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER)
            {
                enet_peer_on_disconnect(peer);
                peer.State = ENetPeerState.ENET_PEER_STATE_DISCONNECTING;
            }
            else
            {
                enet_host_flush(peer.Host);
                enet_peer_reset(peer);
            }
        }

        void enet_peer_on_disconnect(ENetPeer peer)
        {
            if (peer.State == ENetPeerState.ENET_PEER_STATE_CONNECTED ||
                peer.State == ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER)
            {
                // todo compare HOST
                if (peer.IncomingBandwidth != 0)
                {
                    // -- peer.Host._bandwidthLimitedPeers -> bandwidthLimitedPeers;
                    _bandwidthLimitedPeers--;
                }

                _connectedPeers--;
                // -- peer -> host -> connectedPeers;
            }
        }

        void enet_host_flush(ENetHost host)
        {
            _serviceTime = _time.GetTime();
            enet_protocol_send_outgoing_commands(null, false);
        }

        void enet_peer_reset(ENetPeer peer)
        {
            enet_peer_on_disconnect(peer);
            peer.Reset();
        }

        private int enet_socket_send(Socket socket, IPEndPoint address, ENetBuffer[] buffers, uint bufferCount)
        {
            int sendLength;
            try
            {
                // TODO temporary solution, replace by SendToAsync and utilize array of buffers, if possible.
                byte[] data = ENetSocket.Combine(buffers, bufferCount);
                _logger.Debug(
                    $"Send - Len:{data.Length}\r\nData:\r\n{Utility.ToHexString(data, " ")}\r\nAscii:\r\b{Utility.ToAsciiString(data)}");
                sendLength = socket.SendTo(data, 0, data.Length, SocketFlags.None, address);
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.WouldBlock)
                {
                    return 0;
                }

                return -1;
            }

            return sendLength;
        }

        int enet_socket_receive(Socket socket, ref IPEndPoint address, ENetBuffer buffer)
        {
            EndPoint senderRemote = address;
            SocketFlags flags = SocketFlags.None;
            int receivedLength;
            try
            {
                receivedLength = _socket.ReceiveFrom(buffer.Data, flags, ref senderRemote);
                address = (IPEndPoint) senderRemote;
                _logger.Debug(
                    $"Recv - Len:{receivedLength}\r\nData:\r\n{Utility.ToHexString(buffer.Data.SliceEx(0, receivedLength), " ")}\r\nAscii:\r\b{Utility.ToAsciiString(buffer.Data.SliceEx(0, receivedLength))}");
            }
            catch (SocketException ex)
            {
                if (ex.SocketErrorCode == SocketError.WouldBlock
                    || ex.SocketErrorCode == SocketError.ConnectionReset)
                {
                    return 0;
                }

                return -1;
            }

            if (flags.HasFlag(SocketFlags.Partial))
            {
                return -1;
            }

            return receivedLength;
        }

        void enet_peer_ping(ENetPeer peer)
        {
            if (peer.State != ENetPeerState.ENET_PEER_STATE_CONNECTED)
            {
                return;
            }

            ENetProtocol command = new ENetProtocol();
            command.Header.Command = (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_PING |
                                     (byte) ENetProtocolFlag.ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE;
            command.Header.ChannelId = 0xFF;

            enet_peer_queue_outgoing_command(peer, command, null, 0, 0);
        }

        private void enet_protocol_send_acknowledgements(ENetPeer peer)
        {
            List<ENetAcknowledgement> acknowledgements = new List<ENetAcknowledgement>(peer.Acknowledgements);
            foreach (ENetAcknowledgement acknowledgement in acknowledgements)
            {
                if (_commandCount >= _commands.Length
                    || _bufferCount >= _buffers.Length
                    || peer.Mtu - _packetSize < ENetProtocolAcknowledge.Size)
                {
                    _continueSending = true;
                    break;
                }

                ENetProtocol command = _commands[_commandCount];
                ENetBuffer buffer = _buffers[_bufferCount];

                ushort reliableSequenceNumber =
                    ENetSocket.ENET_HOST_TO_NET_16(acknowledgement.Command.Header.ReliableSequenceNumber);

                command.Header.Command = (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_ACKNOWLEDGE;
                command.Header.ChannelId = acknowledgement.Command.Header.ChannelId;
                command.Header.ReliableSequenceNumber = reliableSequenceNumber;
                command.Acknowledge.ReceivedReliableSequenceNumber = reliableSequenceNumber;
                command.Acknowledge.ReceivedSentTime =
                    ENetSocket.ENET_HOST_TO_NET_16((ushort) acknowledgement.SentTime);

                buffer.Data = Serialize(command);
                buffer.DataLength = ENetProtocolAcknowledge.Size;

                _packetSize += buffer.DataLength;

                if ((acknowledgement.Command.Header.Command & (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_MASK) ==
                    (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_DISCONNECT)
                {
                    enet_protocol_dispatch_state(peer, ENetPeerState.ENET_PEER_STATE_ZOMBIE);
                }

                peer.Acknowledgements.Remove(acknowledgement);

                _commandCount++;
                _bufferCount++;
            }
        }

        private void enet_protocol_dispatch_state(ENetPeer peer, ENetPeerState state)
        {
            enet_protocol_change_state(peer, state);
            if (!peer.NeedsDispatch)
            {
                _dispatchQueue.Add(peer);
                peer.NeedsDispatch = true;
            }
        }

        private void enet_protocol_change_state(ENetPeer peer, ENetPeerState state)
        {
            if (state == ENetPeerState.ENET_PEER_STATE_CONNECTED ||
                state == ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER)
            {
                enet_peer_on_connect(peer);
            }
            else
            {
                enet_peer_on_disconnect(peer);
            }

            peer.State = state;
        }

        void enet_peer_on_connect(ENetPeer peer)
        {
            if (peer.State == ENetPeerState.ENET_PEER_STATE_CONNECTED ||
                peer.State == ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER)
            {
                if (peer.IncomingBandwidth != 0)
                {
                    ++_bandwidthLimitedPeers;
                }

                ++_connectedPeers;
            }
        }

        int enet_protocol_send_reliable_outgoing_commands(ENetPeer peer)
        {
            ENetChannel channel;
            ushort reliableWindow;
            uint commandSize;
            bool windowExceeded = false;
            bool windowWrap = false;
            bool canPing = true;

            List<ENetOutgoingCommand> outgoingReliableCommands =
                new List<ENetOutgoingCommand>(peer.OutgoingReliableCommands);
            foreach (ENetOutgoingCommand outgoingCommand in outgoingReliableCommands)
            {
                channel = outgoingCommand.Command.Header.ChannelId < peer.ChannelCount
                    ? peer.Channels[outgoingCommand.Command.Header.ChannelId]
                    : null;
                reliableWindow =
                    (ushort) (outgoingCommand.ReliableSequenceNumber / ENetPeer.ENET_PEER_RELIABLE_WINDOW_SIZE);
                if (channel != null)
                {
                    if (!windowWrap &&
                        outgoingCommand.SendAttempts < 1 &&
                        outgoingCommand.ReliableSequenceNumber % ENetPeer.ENET_PEER_RELIABLE_WINDOW_SIZE == 0 &&
                        (channel.ReliableWindows[
                             (reliableWindow + ENetPeer.ENET_PEER_RELIABLE_WINDOWS - 1) %
                             ENetPeer.ENET_PEER_RELIABLE_WINDOWS] >= ENetPeer.ENET_PEER_RELIABLE_WINDOW_SIZE ||
                         (channel.UsedReliableWindows &
                          ((((1 << (int) ENetPeer.ENET_PEER_FREE_RELIABLE_WINDOWS) - 1) << reliableWindow) |
                           (((1 << (int) ENetPeer.ENET_PEER_FREE_RELIABLE_WINDOWS) - 1) >>
                            ((int) ENetPeer.ENET_PEER_RELIABLE_WINDOWS - reliableWindow)))) != 0
                        )
                    )
                    {
                        windowWrap = true;
                    }

                    if (windowWrap)
                    {
                        continue;
                    }
                }

                if (outgoingCommand.Packet != null)
                {
                    if (!windowExceeded)
                    {
                        uint windowSize =
                            (peer.PacketThrottle * peer.WindowSize) / ENetPeer.ENET_PEER_PACKET_THROTTLE_SCALE;

                        if (peer.ReliableDataInTransit + outgoingCommand.FragmentLength >
                            Math.Max(windowSize, peer.Mtu))
                            windowExceeded = true;
                    }

                    if (windowExceeded)
                    {
                        continue;
                    }
                }

                canPing = false;

                commandSize =
                    (uint) CommandSizes[outgoingCommand.Command.Header.Command
                                        & (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_MASK];

                if (_commandCount >= _commands.Length || _bufferCount + 1 >= _buffers.Length ||
                    peer.Mtu - _packetSize < commandSize ||
                    (outgoingCommand.Packet != null &&
                     (peer.Mtu - _packetSize) < (commandSize + outgoingCommand.FragmentLength))
                )
                {
                    _logger.Debug(String.Format(
                        "enet_protocol_send_reliable_outgoing_commands:: Can not process more packets\r\n" +
                        "Command Pool (Index: {0} >= Size: {1}) || Buffer Pool (Index: {2} >= Size: {3}) ||\r\n" +
                        "Mtu: {4} - PacketSize {5} (={6}) < CommandSize: {7} ||\r\n" +
                        "Mtu: {4} - PacketSize {5} (={6}) < CommandSize: {7} + FragmentLength: {8} (={9})",
                        _commandCount, _commands.Length,
                        _bufferCount + 1, _buffers.Length,
                        peer.Mtu, _packetSize, peer.Mtu - _packetSize, commandSize,
                        outgoingCommand.FragmentLength, commandSize + outgoingCommand.FragmentLength
                    ));
                    _continueSending = true;
                    break;
                }

                if (channel != null && outgoingCommand.SendAttempts < 1)
                {
                    channel.UsedReliableWindows = (ushort) (channel.UsedReliableWindows | 1 << reliableWindow);
                    ++channel.ReliableWindows[reliableWindow];
                }

                ++outgoingCommand.SendAttempts;

                if (outgoingCommand.RoundTripTimeout == 0)
                {
                    outgoingCommand.RoundTripTimeout = peer.RoundTripTime + 4 * peer.RoundTripTimeVariance;
                    outgoingCommand.RoundTripTimeoutLimit = peer.TimeoutLimit * outgoingCommand.RoundTripTimeout;
                }

                if (peer.SentReliableCommands.Count <= 0)
                {
                    peer.NextTimeout = _serviceTime + outgoingCommand.RoundTripTimeout;
                }

                peer.SentReliableCommands.Add(outgoingCommand);
                peer.OutgoingReliableCommands.Remove(outgoingCommand);

                outgoingCommand.SentTime = _serviceTime;


                ENetProtocol command = outgoingCommand.Command;
                _commands[_commandCount] = command;

                ENetProtocolCommand cmd =
                    (ENetProtocolCommand) (command.Header.Command &
                                           (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_MASK);
                _logger.Debug($"Sending Reliable Command: ({(int) cmd}) {cmd}");

                ENetBuffer buffer = _buffers[_bufferCount];
                buffer.Data = Serialize(command);
                buffer.DataLength = commandSize;

                _packetSize += buffer.DataLength;
                _headerFlags |= ENetProtocolFlag.ENET_PROTOCOL_HEADER_FLAG_SENT_TIME;

                if (outgoingCommand.Packet != null)
                {
                    _bufferCount++;
                    ENetBuffer buffer1 = _buffers[_bufferCount];
                    Span<byte> data = outgoingCommand.Packet.Data;
                    buffer1.Data = data.Slice((int) outgoingCommand.FragmentOffset, outgoingCommand.FragmentLength)
                        .ToArray();
                    buffer1.DataLength = outgoingCommand.FragmentLength;

                    _packetSize += outgoingCommand.FragmentLength;
                    peer.ReliableDataInTransit += outgoingCommand.FragmentLength;
                }

                peer.PacketsSent++;

                _commandCount++;
                _bufferCount++;
            }

            return canPing ? 1 : 0;
        }


        void enet_protocol_send_unreliable_outgoing_commands(ENetPeer peer)
        {
            // todo
        }

        int enet_protocol_check_timeouts(ENetPeer peer, ENetEvent eNetEvent)
        {
            // ENetOutgoingCommand outgoingCommand;
            //  ENetListIterator currentCommand, insertPosition;

            //   currentCommand = enet_list_begin(&peer->sentReliableCommands);
            //  insertPosition = enet_list_begin(&peer->outgoingReliableCommands);


            foreach (ENetOutgoingCommand outgoingCommand in peer.SentReliableCommands)
            {
                //   outgoingCommand = (ENetOutgoingCommand) currentCommand;

                //   currentCommand = enet_list_next(currentCommand);

                if (_time.Difference(_serviceTime, outgoingCommand.SentTime) < outgoingCommand.RoundTripTimeout)
                {
                    continue;
                }

                if (peer.EarliestTimeout == 0 || _time.Less(outgoingCommand.SentTime, peer.EarliestTimeout))
                {
                    peer.EarliestTimeout = outgoingCommand.SentTime;
                }

                if (peer.EarliestTimeout != 0 &&
                    (_time.Difference(_serviceTime, peer.EarliestTimeout) >= peer.TimeoutMaximum ||
                     (outgoingCommand.RoundTripTimeout >= outgoingCommand.RoundTripTimeoutLimit &&
                      _time.Difference(_serviceTime, peer.EarliestTimeout) >= peer.TimeoutMinimum)))
                {
                    enet_protocol_notify_disconnect(peer, eNetEvent);
                    return 1;
                }

                if (outgoingCommand.Packet != null)
                {
                    peer.ReliableDataInTransit -= outgoingCommand.FragmentLength;
                }


                ++peer.PacketsLost;

                outgoingCommand.RoundTripTimeout *= 2;

                // enet_list_insert(insertPosition, enet_list_remove(&outgoingCommand->outgoingCommandList));

                //   if (currentCommand == enet_list_begin(&peer->sentReliableCommands) && !enet_list_empty(&peer->sentReliableCommands))
                //      {
                //         outgoingCommand = (ENetOutgoingCommand*) currentCommand;
                //        peer.NextTimeout = outgoingCommand.SentTime + outgoingCommand.RoundTripTimeout;
                //     }
            }

            return 0;
        }

        private void enet_protocol_notify_disconnect(ENetPeer peer, ENetEvent eNetEvent)
        {
            if (peer.State >= ENetPeerState.ENET_PEER_STATE_CONNECTION_PENDING)
            {
                _recalculateBandwidthLimits = true;
            }

            if (peer.State != ENetPeerState.ENET_PEER_STATE_CONNECTING &&
                peer.State < ENetPeerState.ENET_PEER_STATE_CONNECTION_SUCCEEDED)
            {
                enet_peer_reset(peer);
            }
            else if (eNetEvent != null)
            {
                eNetEvent.Type = ENetEventType.ENET_EVENT_TYPE_DISCONNECT;
                eNetEvent.Peer = peer;
                eNetEvent.Data = 0;
                enet_peer_reset(peer);
            }
            else
            {
                peer.EventData = 0;
                enet_protocol_dispatch_state(peer, ENetPeerState.ENET_PEER_STATE_ZOMBIE);
            }
        }

        private int enet_protocol_receive_incoming_commands(ENetEvent eNetEvent)
        {
            int packets;

            for (packets = 0; packets < 256; ++packets)
            {
                ENetBuffer buffer = new ENetBuffer();
                buffer.Data = _packetData[0];
                buffer.DataLength = (uint) _packetData[0].LongLength;

                int receivedLength = enet_socket_receive(_socket, ref _receivedAddress, buffer);

                if (receivedLength < 0)
                    return -1;

                if (receivedLength == 0)
                    return 0;

                _receivedData = _packetData[0];
                _receivedDataLength = (uint) receivedLength;

                _totalReceivedData += (uint) receivedLength;
                _totalReceivedPackets++;

                if (_intercept != null)
                {
                    switch (_intercept.Intercept(this, eNetEvent))
                    {
                        case 1:
                            if (eNetEvent != null && eNetEvent.Type != ENetEventType.ENET_EVENT_TYPE_NONE)
                                return 1;
                            continue;
                        case -1:
                            return -1;
                        default:
                            break;
                    }
                }

                switch (enet_protocol_handle_incoming_commands(eNetEvent))
                {
                    case 1:
                        return 1;
                    case -1:
                        return -1;
                    default:
                        break;
                }
            }

            return -1;
        }

        private int enet_socket_wait(Socket socket, ref ENetSocketWait condition, uint timeout)
        {
            return 0;
        }

        public int enet_protocol_handle_incoming_commands(ENetEvent eNetEvent)
        {
            if (_receivedDataLength < ENetProtocolHeader.Size)
            {
                return 0;
            }

            Span<byte> receivedData = _receivedData;

            ENetProtocolHeader header = Deserialize<ENetProtocolHeader>(
                receivedData.Slice(0, ENetProtocolHeader.Size)
            );
            ushort peerId = ENetSocket.ENET_NET_TO_HOST_16(header.PeerId);
            byte sessionId = (byte) (
                (peerId & (ushort) ENetProtocolFlag.ENET_PROTOCOL_HEADER_SESSION_MASK) >>
                (ushort) ENetProtocolFlag.ENET_PROTOCOL_HEADER_SESSION_SHIFT
            );
            ENetProtocolFlag flags = (ENetProtocolFlag) (
                peerId & (ushort) ENetProtocolFlag.ENET_PROTOCOL_HEADER_FLAG_MASK
            );
            peerId = (ushort) (
                peerId & ~((ushort) ENetProtocolFlag.ENET_PROTOCOL_HEADER_FLAG_MASK |
                           (ushort) ENetProtocolFlag.ENET_PROTOCOL_HEADER_SESSION_MASK)
            );
            uint headerSize = flags.HasFlag(ENetProtocolFlag.ENET_PROTOCOL_HEADER_FLAG_SENT_TIME)
                ? (uint) ENetProtocolHeader.Size
                : (uint) ENetProtocolHeader.SizeWithoutSentTime;

            if (_checksum != null)
            {
                headerSize += ENetProtocolHeader.SizeOfChecksum;
            }

            ENetPeer peer;

            if (peerId == ENET_PROTOCOL_MAXIMUM_PEER_ID)
            {
                peer = null;
            }
            else if (peerId >= _peerCount)
            {
                return 0;
            }
            else
            {
                peer = _peers[peerId];
                if (peer.State == ENetPeerState.ENET_PEER_STATE_DISCONNECTED
                    || peer.State == ENetPeerState.ENET_PEER_STATE_ZOMBIE)
                {
                    _logger.Debug($"Existing peer state: {peer.State} is invalid");
                    return 0;
                }

                if (!ENetSocket.IpEndPointEquals(_receivedAddress, peer.Address) &&
                    !ENetSocket.IpAddressEquals(_receivedAddress.Address, IPAddress.Broadcast)
                )
                {
                    _logger.Debug($"Peer address: {peer.Address} does not match received address: {_receivedAddress}");
                    return 0;
                }

                if (peer.OutgoingPeerId < ENET_PROTOCOL_MAXIMUM_PEER_ID && sessionId != peer.IncomingSessionId)
                {
                    _logger.Debug(
                        $"Peers incomingSessionId: {peer.IncomingSessionId} doesn't match sessionId: {sessionId}");
                    return 0;
                }
            }

            if (flags.HasFlag(ENetProtocolFlag.ENET_PROTOCOL_HEADER_FLAG_COMPRESSED))
            {
                _logger.Debug("Compressed");
            }

            if (_checksum != null)
            {
                uint checksum = BitConverter.ToUInt32(_receivedData,
                    (int) (headerSize - ENetProtocolHeader.SizeOfChecksum));
                uint desiredChecksum = checksum;

                // ENetBuffer buffer;
                // checksum = peer != NULL ? peer->connectID : 0;
                //  buffer.data = host->receivedData;
                //  buffer.dataLength = host->receivedDataLength;

                if (_checksum.Calculate(
                        new ENetBuffer[]
                            {new ENetBuffer() {Data = _receivedData, DataLength = (uint) _receivedData.Length}},
                        0, 1) != desiredChecksum)
                {
                    return 0;
                }
            }

            if (peer != null)
            {
                peer.Address = _receivedAddress;
                peer.IncomingDataTotal += _receivedDataLength;
            }

            _receivedDataOffset = headerSize;
            while (_receivedDataOffset < _receivedDataLength)
            {
                if (_receivedDataOffset + ENetProtocolCommandHeader.Size > _receivedDataLength)
                {
                    break;
                }

                ENetProtocol command = Deserialize<ENetProtocol>(
                    receivedData.Slice((int) _receivedDataOffset)
                );

                ENetProtocolCommand commandNumber = (ENetProtocolCommand) (
                    command.Header.Command & (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_MASK
                );

                if (!Enum.IsDefined(typeof(ENetProtocolCommand), commandNumber))
                {
                    _logger.Debug($"Command: '{commandNumber}' not defined");
                    break;
                }

                if (commandNumber >= ENetProtocolCommand.ENET_PROTOCOL_COMMAND_COUNT)
                {
                    _logger.Debug($"Command: '{command}' exceeds maximum command count");
                    break;
                }

                int commandSize = CommandSizes[(int) commandNumber];
                if (commandSize == 0 || _receivedDataOffset + commandSize > _receivedDataLength)
                {
                    _logger.Debug(
                        $"CommandSize: '{commandSize}' == 0 or receivedDataOffset: '{_receivedDataOffset}' + commandSize:'{commandSize}' (='{_receivedDataOffset + commandSize}') > _receivedDataLength: '{_receivedDataLength}'");
                    break;
                }

                _receivedDataOffset += (uint) commandSize;

                if (peer == null && commandNumber != ENetProtocolCommand.ENET_PROTOCOL_COMMAND_CONNECT)
                {
                    break;
                }

                command.Header.ReliableSequenceNumber =
                    ENetSocket.ENET_NET_TO_HOST_16(command.Header.ReliableSequenceNumber);

                switch (commandNumber)
                {
                    case ENetProtocolCommand.ENET_PROTOCOL_COMMAND_ACKNOWLEDGE:
                    {
                        int result = enet_protocol_handle_acknowledge(eNetEvent, peer, command);
                        _logger.Debug($"handle::ENET_PROTOCOL_COMMAND_ACKNOWLEDGE: {result}");
                        if (result != 0)
                        {
                            return command_error(eNetEvent);
                        }

                        break;
                    }
                    case ENetProtocolCommand.ENET_PROTOCOL_COMMAND_CONNECT:
                    {
                        if (peer != null)
                        {
                            _logger.Debug($"handle::ENET_PROTOCOL_COMMAND_CONNECT: {peer == null}");
                            return command_error(eNetEvent);
                        }

                        peer = enet_protocol_handle_connect(command.Connect);
                        _logger.Debug($"handle::ENET_PROTOCOL_COMMAND_CONNECT: {peer == null}");
                        if (peer == null)
                        {
                            return command_error(eNetEvent);
                        }

                        break;
                    }
                    case ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_RELIABLE:
                    {
                        int result = enet_protocol_handle_send_reliable(peer, command, receivedData);
                        _logger.Debug($"handle::ENET_PROTOCOL_COMMAND_SEND_RELIABLE: {result}");
                        if (result != 0)
                        {
                            return command_error(eNetEvent);
                        }

                        break;
                    }
                    case ENetProtocolCommand.ENET_PROTOCOL_COMMAND_BANDWIDTH_LIMIT:
                    {
                        int result = enet_protocol_handle_bandwidth_limit(peer, command);
                        _logger.Debug($"handle::ENET_PROTOCOL_COMMAND_BANDWIDTH_LIMIT: {result}");
                        if (result != 0)
                        {
                            return command_error(eNetEvent);
                        }

                        break;
                    }
                    default:
                    {
                        _logger.Debug(
                            $"enet_protocol_handle_incoming_commands:: Command Number: {commandNumber} not handled");
                        break;
                    }
                }

                if (peer != null && Bitmask.IsSet(command.Header.Command,
                        (byte) ENetProtocolFlag.ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE))
                {
                    if (!(flags.HasFlag(ENetProtocolFlag.ENET_PROTOCOL_HEADER_FLAG_SENT_TIME)))
                    {
                        _logger.Debug($"Flags: '{flags}' missing ENET_PROTOCOL_HEADER_FLAG_SENT_TIME");
                        break;
                    }

                    ushort sentTime = ENetSocket.ENET_NET_TO_HOST_16(header.SentTime);
                    switch (peer.State)
                    {
                        case ENetPeerState.ENET_PEER_STATE_DISCONNECTING:
                        case ENetPeerState.ENET_PEER_STATE_ACKNOWLEDGING_CONNECT:
                        case ENetPeerState.ENET_PEER_STATE_DISCONNECTED:
                        case ENetPeerState.ENET_PEER_STATE_ZOMBIE:
                        {
                            break;
                        }
                        case ENetPeerState.ENET_PEER_STATE_ACKNOWLEDGING_DISCONNECT:
                        {
                            if (commandNumber == ENetProtocolCommand.ENET_PROTOCOL_COMMAND_DISCONNECT)
                            {
                                enet_peer_queue_acknowledgement(peer, command, sentTime);
                            }

                            break;
                        }
                        default:
                        {
                            enet_peer_queue_acknowledgement(peer, command, sentTime);
                            break;
                        }
                    }
                }
            }

            return command_error(eNetEvent);
        }

        private int command_error(ENetEvent eNetEvent)
        {
            if (eNetEvent != null && eNetEvent.Type != ENetEventType.ENET_EVENT_TYPE_NONE)
            {
                return 1;
            }

            return 0;
        }

        private int enet_protocol_handle_send_reliable(ENetPeer peer, ENetProtocol command, Span<byte> receivedData)
        {
            if (command.Header.ChannelId >= peer.ChannelCount ||
                (peer.State != ENetPeerState.ENET_PEER_STATE_CONNECTED &&
                 peer.State != ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER))
            {
                return -1;
            }

            uint dataLength = ENetSocket.ENET_NET_TO_HOST_16(command.SendReliable.DataLength);
            uint totalLength = _receivedDataOffset + dataLength;
            int receivedDataStart = (int) _receivedDataOffset;
            _receivedDataOffset += dataLength;
            if (dataLength > _maximumPacketSize || totalLength < 0 || totalLength > _receivedDataLength)
            {
                return -1;
            }

            if (enet_peer_queue_incoming_command(peer, command, receivedData.Slice((int) receivedDataStart),
                    dataLength, ENetPacketFlags.ENET_PACKET_FLAG_RELIABLE, 0) == null)
            {
                return -1;
            }

            return 0;
        }

        private ENetIncomingCommand enet_peer_queue_incoming_command(ENetPeer peer, ENetProtocol command,
            Span<byte> data,
            uint dataLength, ENetPacketFlags flags, uint fragmentCount)
        {
            ENetChannel channel = peer.Channels[command.Header.ChannelId];
            uint unreliableSequenceNumber = 0;
            uint reliableSequenceNumber = 0;
            ushort reliableWindow;
            ushort currentWindow;
            ENetPacket packet = null;
            List<ENetIncomingCommand> currentCommandList;
            int currentCommandIndex;

            if (peer.State == ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER)
            {
                goto discardCommand;
            }

            if ((command.Header.Command & (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_MASK) !=
                (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_UNSEQUENCED)
            {
                reliableSequenceNumber = command.Header.ReliableSequenceNumber;
                reliableWindow = (ushort) (reliableSequenceNumber / ENetPeer.ENET_PEER_RELIABLE_WINDOW_SIZE);
                currentWindow =
                    (ushort) (channel.IncomingReliableSequenceNumber / ENetPeer.ENET_PEER_RELIABLE_WINDOW_SIZE);

                if (reliableSequenceNumber < channel.IncomingReliableSequenceNumber)
                {
                    reliableWindow = (ushort) (reliableWindow + ENetPeer.ENET_PEER_RELIABLE_WINDOWS);
                }

                if (reliableWindow < currentWindow ||
                    reliableWindow >= currentWindow + ENetPeer.ENET_PEER_FREE_RELIABLE_WINDOWS - 1)
                {
                    goto discardCommand;
                }
            }

            switch ((ENetProtocolCommand) command.Header.Command & ENetProtocolCommand.ENET_PROTOCOL_COMMAND_MASK)
            {
                case ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_FRAGMENT:
                case ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_RELIABLE:
                {
                    if (reliableSequenceNumber == channel.IncomingReliableSequenceNumber)
                    {
                        goto discardCommand;
                    }

                    currentCommandList = channel.IncomingReliableCommands;
                    for (currentCommandIndex = currentCommandList.Count - 1;
                        currentCommandIndex >= 0;
                        currentCommandIndex--)
                    {
                        ENetIncomingCommand incomingCommand = currentCommandList[currentCommandIndex];
                        if (reliableSequenceNumber >= channel.IncomingReliableSequenceNumber)
                        {
                            if (incomingCommand.ReliableSequenceNumber < channel.IncomingReliableSequenceNumber)
                            {
                                continue;
                            }
                        }
                        else if (incomingCommand.ReliableSequenceNumber >= channel.IncomingReliableSequenceNumber)
                        {
                            break;
                        }

                        if (incomingCommand.ReliableSequenceNumber <= reliableSequenceNumber)
                        {
                            if (incomingCommand.ReliableSequenceNumber < reliableSequenceNumber)
                            {
                                break;
                            }


                            goto discardCommand;
                        }
                    }

                    break;
                }
                case ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE:
                case ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE_FRAGMENT:
                {
                    unreliableSequenceNumber =
                        ENetSocket.ENET_NET_TO_HOST_16(command.SendUnreliable.UnreliableSequenceNumber);

                    if (reliableSequenceNumber == channel.IncomingReliableSequenceNumber &&
                        unreliableSequenceNumber <= channel.IncomingUnreliableSequenceNumber)
                    {
                        goto discardCommand;
                    }

                    currentCommandList = channel.IncomingUnreliableCommands;
                    for (currentCommandIndex = currentCommandList.Count - 1;
                        currentCommandIndex >= 0;
                        currentCommandIndex--)
                    {
                        ENetIncomingCommand incomingCommand = currentCommandList[currentCommandIndex];

                        if (((ENetProtocolCommand) command.Header.Command &
                             ENetProtocolCommand.ENET_PROTOCOL_COMMAND_MASK) ==
                            ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_UNSEQUENCED)
                        {
                            continue;
                        }

                        if (reliableSequenceNumber >= channel.IncomingReliableSequenceNumber)
                        {
                            if (incomingCommand.ReliableSequenceNumber < channel.IncomingReliableSequenceNumber)
                            {
                                continue;
                            }
                        }
                        else if (incomingCommand.ReliableSequenceNumber >= channel.IncomingReliableSequenceNumber)
                        {
                            break;
                        }

                        if (incomingCommand.ReliableSequenceNumber < reliableSequenceNumber)
                        {
                            break;
                        }

                        if (incomingCommand.ReliableSequenceNumber > reliableSequenceNumber)
                        {
                            continue;
                        }

                        if (incomingCommand.UnreliableSequenceNumber <= unreliableSequenceNumber)
                        {
                            if (incomingCommand.UnreliableSequenceNumber < unreliableSequenceNumber)
                            {
                                break;
                            }

                            goto discardCommand;
                        }
                    }

                    break;
                }
                case ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_UNSEQUENCED:
                {
                    // Insert at end
                    currentCommandList = channel.IncomingUnreliableCommands;
                    currentCommandIndex = channel.IncomingUnreliableCommands.Count - 1;
                    break;
                }
                default:
                {
                    goto discardCommand;
                }
            }

            if (peer.TotalWaitingData >= _maximumWaitingData)
            {
                goto notifyError;
            }

            packet = new ENetPacket(data.Slice(0, (int) dataLength).ToArray(), dataLength, flags);
            if (packet == null)
            {
                goto notifyError;
            }

            ENetIncomingCommand newIncomingCommand = new ENetIncomingCommand();
            if (newIncomingCommand == null)
            {
                goto notifyError;
            }

            newIncomingCommand.ReliableSequenceNumber = command.Header.ReliableSequenceNumber;
            _logger.Debug($"incomingCommand.ReliableSequenceNumber  = {command.Header.ReliableSequenceNumber}");
            newIncomingCommand.UnreliableSequenceNumber = (ushort) unreliableSequenceNumber; //& 0xFFFF;
            newIncomingCommand.Command = command;
            newIncomingCommand.FragmentCount = fragmentCount;
            newIncomingCommand.FragmentsRemaining = fragmentCount;
            newIncomingCommand.Packet = packet;
            newIncomingCommand.Fragments = null;

            if (fragmentCount > 0)
            {
                if (fragmentCount <= ENET_PROTOCOL_MAXIMUM_FRAGMENT_COUNT)
                {
                    // incomingCommand -> fragments = (enet_uint32 *) enet_malloc ((fragmentCount + 31) / 32 * sizeof (enet_uint32));
                    newIncomingCommand.Fragments = new uint[(fragmentCount + 31) / 32 * 4];
                }

                if (newIncomingCommand.Fragments == null)
                {
                    //  enet_free(incomingCommand);
                    goto notifyError;
                }

                //memset(incomingCommand->fragments, 0, (fragmentCount + 31) / 32 * sizeof(enet_uint32));
            }

            if (packet != null)
            {
                ++packet.ReferenceCount;
                peer.TotalWaitingData += packet.DataLength;
            }

            currentCommandIndex++;
            _logger.Debug($"Inserting command at {currentCommandIndex} of {currentCommandList.Count}");
            currentCommandList.Insert(currentCommandIndex, newIncomingCommand);

            switch ((ENetProtocolCommand) command.Header.Command & ENetProtocolCommand.ENET_PROTOCOL_COMMAND_MASK)
            {
                case ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_FRAGMENT:
                case ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_RELIABLE:
                {
                    enet_peer_dispatch_incoming_reliable_commands(peer, channel);
                    break;
                }
                default:
                {
                    _logger.Error("TODO enet_peer_dispatch_incoming_unreliable_commands");
                    // enet_peer_dispatch_incoming_unreliable_commands(peer, channel);
                    break;
                }
            }

            return newIncomingCommand;

            discardCommand:
            if (fragmentCount > 0)
            {
                goto notifyError;
            }

            if (packet != null && packet.ReferenceCount == 0)
            {
                // enet_packet_destroy(packet);
            }

            // todo static instance / decrease instanciation ?
            return new ENetIncomingCommand();
            //return &dummyCommand;

            notifyError:
            if (packet != null && packet.ReferenceCount == 0)
            {
                // enet_packet_destroy(packet);
            }

            return null;
        }

        private void enet_peer_dispatch_incoming_reliable_commands(ENetPeer peer, ENetChannel channel)
        {
            ENetIncomingCommand currentCommand = null;

            foreach (ENetIncomingCommand incomingCommand in channel.IncomingReliableCommands)
            {
                currentCommand = incomingCommand;
                if (incomingCommand.FragmentsRemaining > 0 ||
                    incomingCommand.ReliableSequenceNumber != (channel.IncomingReliableSequenceNumber + 1)
                )
                {
                    break;
                }

                channel.IncomingReliableSequenceNumber = incomingCommand.ReliableSequenceNumber;

                if (incomingCommand.FragmentCount > 0)
                {
                    channel.IncomingReliableSequenceNumber += (ushort) (incomingCommand.FragmentCount - 1);
                }
            }

            //   if (currentCommand == enet_list_begin (& channel -> incomingReliableCommands))
            if (currentCommand == null)
            {
                return;
            }

            channel.IncomingUnreliableSequenceNumber = 0;

            //  enet_list_move(
            //      enet_list_end(peer.DispatchedCommands),
            //      enet_list_begin(channel.IncomingReliableCommands), 
            //      enet_list_previous(currentCommand)
            //      );
            peer.DispatchedCommands.Add(currentCommand);
            channel.IncomingReliableCommands.Remove(currentCommand);

            if (!peer.NeedsDispatch)
            {
                _dispatchQueue.Add(peer);
                // enet_list_insert(enet_list_end(_dispatchQueue), peer.DispatchList);
                peer.NeedsDispatch = true;
            }

            if (channel.IncomingUnreliableCommands.Count > 0)
            {
                _logger.Error("TODO enet_peer_dispatch_incoming_unreliable_commands");
                // enet_peer_dispatch_incoming_unreliable_commands(peer, channel);
            }
        }

        private int enet_protocol_handle_acknowledge(ENetEvent eNetEvent, ENetPeer peer, ENetProtocol command)
        {
            uint roundTripTime;
            uint receivedSentTime;
            ushort receivedReliableSequenceNumber;
            ENetProtocolCommand commandNumber;

            if (peer.State == ENetPeerState.ENET_PEER_STATE_DISCONNECTED ||
                peer.State == ENetPeerState.ENET_PEER_STATE_ZOMBIE)
            {
                return 0;
            }

            receivedSentTime = ENetSocket.ENET_NET_TO_HOST_16(command.Acknowledge.ReceivedSentTime);
            receivedSentTime = receivedSentTime | _serviceTime & 0xFFFF0000;
            if ((receivedSentTime & 0x8000) > (_serviceTime & 0x8000))
            {
                receivedSentTime -= 0x10000;
            }

            if (_time.Less(_serviceTime, receivedSentTime))
            {
                return 0;
            }

            peer.LastReceiveTime = _serviceTime;
            peer.EarliestTimeout = 0;

            roundTripTime = _time.Difference(_serviceTime, receivedSentTime);

            enet_peer_throttle(peer, roundTripTime);

            peer.RoundTripTimeVariance -= peer.RoundTripTimeVariance / 4;

            if (roundTripTime >= peer.RoundTripTime)
            {
                peer.RoundTripTime += (roundTripTime - peer.RoundTripTime) / 8;
                peer.RoundTripTimeVariance += (roundTripTime - peer.RoundTripTime) / 4;
            }
            else
            {
                peer.RoundTripTime -= (peer.RoundTripTime - roundTripTime) / 8;
                peer.RoundTripTimeVariance += (peer.RoundTripTime - roundTripTime) / 4;
            }

            if (peer.RoundTripTime < peer.LowestRoundTripTime)
            {
                peer.LowestRoundTripTime = peer.RoundTripTime;
            }

            if (peer.RoundTripTimeVariance > peer.HighestRoundTripTimeVariance)
            {
                peer.HighestRoundTripTimeVariance = peer.RoundTripTimeVariance;
            }

            if (peer.PacketThrottleEpoch == 0 ||
                _time.Difference(_serviceTime, peer.PacketThrottleEpoch) >= peer.PacketThrottleInterval)
            {
                peer.LastRoundTripTime = peer.LowestRoundTripTime;
                peer.LastRoundTripTimeVariance = peer.HighestRoundTripTimeVariance;
                peer.LowestRoundTripTime = peer.RoundTripTime;
                peer.HighestRoundTripTimeVariance = peer.RoundTripTimeVariance;
                peer.PacketThrottleEpoch = _serviceTime;
            }

            receivedReliableSequenceNumber =
                ENetSocket.ENET_NET_TO_HOST_16(command.Acknowledge.ReceivedReliableSequenceNumber);

            commandNumber =
                enet_protocol_remove_sent_reliable_command(peer, receivedReliableSequenceNumber,
                    command.Header.ChannelId);

            _logger.Debug($"commandNumber: {commandNumber}");

            switch (peer.State)
            {
                case ENetPeerState.ENET_PEER_STATE_ACKNOWLEDGING_CONNECT:
                {
                    if (commandNumber != ENetProtocolCommand.ENET_PROTOCOL_COMMAND_VERIFY_CONNECT)
                    {
                        return -1;
                    }

                    enet_protocol_notify_connect(peer, eNetEvent);
                    break;
                }
                case ENetPeerState.ENET_PEER_STATE_DISCONNECTING:
                {
                    if (commandNumber != ENetProtocolCommand.ENET_PROTOCOL_COMMAND_DISCONNECT)
                    {
                        return -1;
                    }

                    enet_protocol_notify_disconnect(peer, eNetEvent);
                    break;
                }
                case ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER:
                {
                    if (peer.OutgoingReliableCommands.Count <= 0 && peer.OutgoingUnreliableCommands.Count <= 0 &&
                        peer.SentReliableCommands.Count <= 0)
                    {
                        enet_peer_disconnect(peer, peer.EventData);
                    }

                    break;
                }
                default:
                {
                    break;
                }
            }

            return 0;
        }

        private void enet_protocol_notify_connect(ENetPeer peer, ENetEvent eNetEvent)
        {
            _recalculateBandwidthLimits = true;
            if (eNetEvent != null)
            {
                enet_protocol_change_state(peer, ENetPeerState.ENET_PEER_STATE_CONNECTED);
                eNetEvent.Type = ENetEventType.ENET_EVENT_TYPE_CONNECT;
                eNetEvent.Peer = peer;
                eNetEvent.Data = peer.EventData;
            }
            else
            {
                enet_protocol_dispatch_state(peer, peer.State == ENetPeerState.ENET_PEER_STATE_CONNECTING
                    ? ENetPeerState.ENET_PEER_STATE_CONNECTION_SUCCEEDED
                    : ENetPeerState.ENET_PEER_STATE_CONNECTION_PENDING);
            }
        }

        private ENetProtocolCommand enet_protocol_remove_sent_reliable_command(ENetPeer peer,
            ushort reliableSequenceNumber, byte channelId)
        {
            ENetOutgoingCommand outgoingCommand = null;
            int currentCommandIndex = 0;
            ENetProtocolCommand commandNumber;
            bool wasSent = true;

            for (; currentCommandIndex < peer.SentReliableCommands.Count; currentCommandIndex++)
            {
                outgoingCommand = peer.SentReliableCommands[currentCommandIndex];
                if (outgoingCommand.ReliableSequenceNumber == reliableSequenceNumber &&
                    outgoingCommand.Command.Header.ChannelId == channelId)
                {
                    break;
                }
            }

            if (currentCommandIndex + 1 == peer.SentReliableCommands.Count)
            {
                for (currentCommandIndex = 0;
                    currentCommandIndex < peer.OutgoingReliableCommands.Count;
                    currentCommandIndex++)
                {
                    outgoingCommand = peer.OutgoingReliableCommands[currentCommandIndex];

                    if (outgoingCommand.SendAttempts < 1)
                    {
                        return ENetProtocolCommand.ENET_PROTOCOL_COMMAND_NONE;
                    }

                    if (outgoingCommand.ReliableSequenceNumber == reliableSequenceNumber &&
                        outgoingCommand.Command.Header.ChannelId == channelId)
                    {
                        break;
                    }
                }

                if (currentCommandIndex + 1 == peer.OutgoingReliableCommands.Count)
                {
                }

                wasSent = false;
            }

            if (outgoingCommand == null)
            {
                return ENetProtocolCommand.ENET_PROTOCOL_COMMAND_NONE;
            }

            if (channelId < peer.ChannelCount)
            {
                ENetChannel channel = peer.Channels[channelId];
                ushort reliableWindow = (ushort) (reliableSequenceNumber / ENetPeer.ENET_PEER_RELIABLE_WINDOW_SIZE);
                if (channel.ReliableWindows[reliableWindow] > 0)
                {
                    --channel.ReliableWindows[reliableWindow];
                    if (channel.ReliableWindows[reliableWindow] == 0)
                    {
                        channel.UsedReliableWindows = (ushort) (channel.UsedReliableWindows & ~ (1 << reliableWindow));
                    }
                }
            }

            commandNumber =
                (ENetProtocolCommand) (outgoingCommand.Command.Header.Command &
                                       (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_MASK);


            // enet_list_remove(outgoingCommand->outgoingCommandList);
            peer.SentReliableCommands.Remove(outgoingCommand);

            if (outgoingCommand.Packet != null)
            {
                if (wasSent)
                {
                    peer.ReliableDataInTransit -= outgoingCommand.FragmentLength;
                }

                --outgoingCommand.Packet.ReferenceCount;

                if (outgoingCommand.Packet.ReferenceCount == 0)
                {
                    outgoingCommand.Packet.Flags |= ENetPacketFlags.ENET_PACKET_FLAG_SENT;
                    // enet_packet_destroy(outgoingCommand->packet);
                }
            }

            outgoingCommand = null;

            if (peer.SentReliableCommands.Count <= 0)
            {
                return commandNumber;
            }

            outgoingCommand = peer.SentReliableCommands[0];
            peer.NextTimeout = outgoingCommand.SentTime + outgoingCommand.RoundTripTimeout;
            return commandNumber;
        }

        private int enet_protocol_handle_bandwidth_limit(ENetPeer peer, ENetProtocol command)
        {
            if (peer.State != ENetPeerState.ENET_PEER_STATE_CONNECTED &&
                peer.State != ENetPeerState.ENET_PEER_STATE_DISCONNECT_LATER)
            {
                return -1;
            }

            if (peer.IncomingBandwidth != 0)
            {
                --_bandwidthLimitedPeers;
            }

            peer.IncomingBandwidth = ENetSocket.ENET_NET_TO_HOST_32(command.BandwidthLimit.IncomingBandwidth);
            peer.OutgoingBandwidth = ENetSocket.ENET_NET_TO_HOST_32(command.BandwidthLimit.OutgoingBandwidth);

            if (peer.IncomingBandwidth != 0)
            {
                ++_bandwidthLimitedPeers;
            }

            if (peer.IncomingBandwidth == 0 && _outgoingBandwidth == 0)
            {
                peer.WindowSize = ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;
            }
            else if (peer.IncomingBandwidth == 0 || _outgoingBandwidth == 0)
            {
                peer.WindowSize = (Math.Max(peer.IncomingBandwidth, _outgoingBandwidth) /
                                   ENetPeer.ENET_PEER_WINDOW_SIZE_SCALE) * ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;
            }
            else
            {
                peer.WindowSize = (Math.Min(peer.IncomingBandwidth, _outgoingBandwidth) /
                                   ENetPeer.ENET_PEER_WINDOW_SIZE_SCALE) * ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;
            }

            if (peer.WindowSize < ENET_PROTOCOL_MINIMUM_WINDOW_SIZE)
            {
                peer.WindowSize = ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;
            }
            else if (peer.WindowSize > ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE)
            {
                peer.WindowSize = ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;
            }

            return 0;
        }

        private int enet_peer_throttle(ENetPeer peer, uint rtt)
        {
            if (peer.LastRoundTripTime <= peer.LastRoundTripTimeVariance)
            {
                peer.PacketThrottle = peer.PacketThrottleLimit;
            }
            else if (rtt < peer.LastRoundTripTime)
            {
                peer.PacketThrottle += peer.PacketThrottleAcceleration;

                if (peer.PacketThrottle > peer.PacketThrottleLimit)
                {
                    peer.PacketThrottle = peer.PacketThrottleLimit;
                }

                return 1;
            }
            else if (rtt > peer.LastRoundTripTime + 2 * peer.LastRoundTripTimeVariance)
            {
                if (peer.PacketThrottle > peer.PacketThrottleDeceleration)
                {
                    peer.PacketThrottle -= peer.PacketThrottleDeceleration;
                }
                else
                {
                    peer.PacketThrottle = 0;
                }

                return -1;
            }

            return 0;
        }

        private ENetPeer enet_protocol_handle_connect(ENetProtocolConnect command)
        {
            uint channelCount = ENetSocket.ENET_NET_TO_HOST_32(command.ChannelCount);

            if (channelCount < ENET_PROTOCOL_MINIMUM_CHANNEL_COUNT ||
                channelCount > ENET_PROTOCOL_MAXIMUM_CHANNEL_COUNT)
            {
                _logger.Debug(
                    $"channelCount: '{channelCount}' < minimum: '{ENET_PROTOCOL_MINIMUM_CHANNEL_COUNT}' or > maximum: '{ENET_PROTOCOL_MAXIMUM_CHANNEL_COUNT}'");
                return null;
            }

            ENetPeer peer = null;
            uint duplicatePeers = 0;
            for (int i = 0; i < _peers.Count; i++)
            {
                ENetPeer currentPeer = _peers[i];
                if (currentPeer.State == ENetPeerState.ENET_PEER_STATE_DISCONNECTED)
                {
                    if (peer == null)
                    {
                        peer = currentPeer;
                    }
                }
                else if (currentPeer.State != ENetPeerState.ENET_PEER_STATE_CONNECTING &&
                         ENetSocket.IpAddressEquals(currentPeer.Address.Address, _receivedAddress.Address))
                {
                    if (currentPeer.Address.Port == _receivedAddress.Port &&
                        currentPeer.ConnectId == command.ConnectId)
                    {
                        return null;
                    }

                    ++duplicatePeers;
                }
            }

            if (peer == null || duplicatePeers >= _duplicatePeers)
            {
                return null;
            }

            if (channelCount > _channelLimit)
            {
                channelCount = _channelLimit;
            }

            peer.Channels.Clear();
            peer.ChannelCount = channelCount;
            peer.State = ENetPeerState.ENET_PEER_STATE_ACKNOWLEDGING_CONNECT;
            peer.ConnectId = command.ConnectId;
            peer.Address = _receivedAddress;
            peer.OutgoingPeerId = ENetSocket.ENET_NET_TO_HOST_16(command.OutgoingPeerId);
            peer.IncomingBandwidth = ENetSocket.ENET_NET_TO_HOST_32(command.IncomingBandwidth);
            peer.OutgoingBandwidth = ENetSocket.ENET_NET_TO_HOST_32(command.OutgoingBandwidth);
            peer.PacketThrottleInterval = ENetSocket.ENET_NET_TO_HOST_32(command.PacketThrottleInterval);
            peer.PacketThrottleAcceleration = ENetSocket.ENET_NET_TO_HOST_32(command.PacketThrottleAcceleration);
            peer.PacketThrottleDeceleration = ENetSocket.ENET_NET_TO_HOST_32(command.PacketThrottleDeceleration);
            peer.EventData = ENetSocket.ENET_NET_TO_HOST_32(command.Data);

            byte incomingSessionID = command.IncomingSessionId == 0xFF
                ? peer.OutgoingSessionId
                : command.IncomingSessionId;
            incomingSessionID = (byte) ((incomingSessionID + 1) &
                                        ((int) ENetProtocolFlag.ENET_PROTOCOL_HEADER_SESSION_MASK >>
                                         (int) ENetProtocolFlag.ENET_PROTOCOL_HEADER_SESSION_SHIFT));
            if (incomingSessionID == peer.OutgoingSessionId)
            {
                incomingSessionID = (byte) ((incomingSessionID + 1) &
                                            ((int) ENetProtocolFlag.ENET_PROTOCOL_HEADER_SESSION_MASK >>
                                             (int) ENetProtocolFlag.ENET_PROTOCOL_HEADER_SESSION_SHIFT));
            }

            peer.OutgoingSessionId = incomingSessionID;

            byte outgoingSessionID = command.OutgoingSessionId == 0xFF
                ? peer.IncomingSessionId
                : command.OutgoingSessionId;
            outgoingSessionID = (byte) ((outgoingSessionID + 1) &
                                        ((int) ENetProtocolFlag.ENET_PROTOCOL_HEADER_SESSION_MASK >>
                                         (int) ENetProtocolFlag.ENET_PROTOCOL_HEADER_SESSION_SHIFT));
            if (outgoingSessionID == peer.IncomingSessionId)
                outgoingSessionID = (byte) ((outgoingSessionID + 1) &
                                            ((int) ENetProtocolFlag.ENET_PROTOCOL_HEADER_SESSION_MASK >>
                                             (int) ENetProtocolFlag.ENET_PROTOCOL_HEADER_SESSION_SHIFT));
            peer.IncomingSessionId = outgoingSessionID;

            for (int i = 0; i < peer.ChannelCount; i++)
            {
                ENetChannel channel = new ENetChannel();
                channel.OutgoingReliableSequenceNumber = 0;
                channel.OutgoingUnreliableSequenceNumber = 0;
                channel.IncomingReliableSequenceNumber = 0;
                channel.IncomingUnreliableSequenceNumber = 0;
                channel.IncomingReliableCommands.Clear();
                channel.IncomingUnreliableCommands.Clear();
                channel.UsedReliableWindows = 0;
                //channel.ReliableWindows.Clear();
                peer.Channels.Add(channel);
            }

            uint mtu = ENetSocket.ENET_NET_TO_HOST_32(command.Mtu);
            if (mtu < ENET_PROTOCOL_MINIMUM_MTU)
            {
                mtu = ENET_PROTOCOL_MINIMUM_MTU;
            }
            else if (mtu > ENET_PROTOCOL_MAXIMUM_MTU)
            {
                mtu = ENET_PROTOCOL_MAXIMUM_MTU;
            }

            peer.Mtu = mtu;

            if (_outgoingBandwidth == 0 && peer.IncomingBandwidth == 0)
            {
                peer.WindowSize = ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;
            }
            else if (_outgoingBandwidth == 0 || peer.IncomingBandwidth == 0)
            {
                peer.WindowSize =
                    (Math.Max(_outgoingBandwidth, peer.IncomingBandwidth) / ENetPeer.ENET_PEER_WINDOW_SIZE_SCALE) *
                    ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;
            }
            else
            {
                peer.WindowSize = (Math.Min(_outgoingBandwidth, peer.IncomingBandwidth) /
                                   ENetPeer.ENET_PEER_WINDOW_SIZE_SCALE) * ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;
            }

            if (peer.WindowSize < ENET_PROTOCOL_MINIMUM_WINDOW_SIZE)
            {
                peer.WindowSize = ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;
            }
            else if (peer.WindowSize > ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE)
            {
                peer.WindowSize = ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;
            }

            uint windowSize;
            if (_incomingBandwidth == 0)
            {
                windowSize = ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;
            }
            else
            {
                windowSize = (_incomingBandwidth / ENetPeer.ENET_PEER_WINDOW_SIZE_SCALE) *
                             ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;
            }

            if (windowSize > ENetSocket.ENET_NET_TO_HOST_32(command.WindowSize))
            {
                windowSize = ENetSocket.ENET_NET_TO_HOST_32(command.WindowSize);
            }

            if (windowSize < ENET_PROTOCOL_MINIMUM_WINDOW_SIZE)
            {
                windowSize = ENET_PROTOCOL_MINIMUM_WINDOW_SIZE;
            }
            else if (windowSize > ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE)
            {
                windowSize = ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;
            }


            ENetProtocol VerifyCommand = new ENetProtocol();
            VerifyCommand.VerifyConnect.Header.Command =
                (int) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_VERIFY_CONNECT |
                (int) ENetProtocolFlag.ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE;
            VerifyCommand.VerifyConnect.Header.ChannelId = 0xFF;
            VerifyCommand.VerifyConnect.OutgoingPeerId = ENetSocket.ENET_HOST_TO_NET_16(peer.IncomingPeerId);
            VerifyCommand.VerifyConnect.IncomingSessionId = incomingSessionID;
            VerifyCommand.VerifyConnect.OutgoingSessionId = outgoingSessionID;
            VerifyCommand.VerifyConnect.Mtu = ENetSocket.ENET_HOST_TO_NET_32(peer.Mtu);
            VerifyCommand.VerifyConnect.WindowSize = ENetSocket.ENET_HOST_TO_NET_32(windowSize);
            VerifyCommand.VerifyConnect.ChannelCount = ENetSocket.ENET_HOST_TO_NET_32(channelCount);
            VerifyCommand.VerifyConnect.IncomingBandwidth = ENetSocket.ENET_HOST_TO_NET_32(_incomingBandwidth);
            VerifyCommand.VerifyConnect.OutgoingBandwidth = ENetSocket.ENET_HOST_TO_NET_32(_outgoingBandwidth);
            VerifyCommand.VerifyConnect.PacketThrottleInterval =
                ENetSocket.ENET_HOST_TO_NET_32(peer.PacketThrottleInterval);
            VerifyCommand.VerifyConnect.PacketThrottleAcceleration =
                ENetSocket.ENET_HOST_TO_NET_32(peer.PacketThrottleAcceleration);
            VerifyCommand.VerifyConnect.PacketThrottleDeceleration =
                ENetSocket.ENET_HOST_TO_NET_32(peer.PacketThrottleDeceleration);
            VerifyCommand.VerifyConnect.ConnectId = peer.ConnectId;

            enet_peer_queue_outgoing_command(peer, VerifyCommand, null, 0, 0);

            return peer;
        }

        public ENetOutgoingCommand enet_peer_queue_outgoing_command(ENetPeer peer, ENetProtocol command,
            ENetPacket packet,
            uint offset, ushort length)
        {
            ENetOutgoingCommand outgoingCommand = new ENetOutgoingCommand();

            outgoingCommand.Command = command;
            outgoingCommand.FragmentOffset = offset;
            outgoingCommand.FragmentLength = length;
            outgoingCommand.Packet = packet;
            if (packet != null)
            {
                packet.ReferenceCount++;
            }

            enet_peer_setup_outgoing_command(peer, outgoingCommand);

            return outgoingCommand;
        }

        public void enet_peer_setup_outgoing_command(ENetPeer peer, ENetOutgoingCommand outgoingCommand)
        {
            peer.OutgoingDataTotal += enet_protocol_command_size(outgoingCommand.Command.Header.Command) +
                                      outgoingCommand.FragmentLength;

            if (outgoingCommand.Command.Header.ChannelId == 0xFF)
            {
                peer.OutgoingReliableSequenceNumber++;
                outgoingCommand.ReliableSequenceNumber = peer.OutgoingReliableSequenceNumber;
                outgoingCommand.UnreliableSequenceNumber = 0;
            }
            else if (Bitmask.IsSet(outgoingCommand.Command.Header.Command,
                (byte) ENetProtocolFlag.ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE))
            {
                ENetChannel channel = peer.Channels[outgoingCommand.Command.Header.ChannelId];

                channel.OutgoingReliableSequenceNumber++;
                channel.OutgoingUnreliableSequenceNumber = 0;
                outgoingCommand.ReliableSequenceNumber = channel.OutgoingReliableSequenceNumber;
                outgoingCommand.UnreliableSequenceNumber = 0;
            }
            else if (Bitmask.IsSet(outgoingCommand.Command.Header.Command,
                (byte) ENetProtocolFlag.ENET_PROTOCOL_COMMAND_FLAG_UNSEQUENCED))
            {
                peer.OutgoingUnsequencedGroup++;
                outgoingCommand.ReliableSequenceNumber = 0;
                outgoingCommand.UnreliableSequenceNumber = 0;
            }
            else
            {
                ENetChannel channel = peer.Channels[outgoingCommand.Command.Header.ChannelId];
                if (outgoingCommand.FragmentOffset == 0)
                {
                    channel.OutgoingUnreliableSequenceNumber++;
                }

                outgoingCommand.ReliableSequenceNumber = channel.OutgoingReliableSequenceNumber;
                outgoingCommand.UnreliableSequenceNumber = channel.OutgoingUnreliableSequenceNumber;
            }

            outgoingCommand.SendAttempts = 0;
            outgoingCommand.SentTime = 0;
            outgoingCommand.RoundTripTimeout = 0;
            outgoingCommand.RoundTripTimeoutLimit = 0;
            outgoingCommand.Command.Header.ReliableSequenceNumber =
                ENetSocket.ENET_HOST_TO_NET_16(outgoingCommand.ReliableSequenceNumber);
            _logger.Debug($"outgoingCommand.ReliableSequenceNumber = {outgoingCommand.ReliableSequenceNumber}");

            switch (outgoingCommand.Command.Header.Command & (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_MASK)
            {
                case (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE:
                    outgoingCommand.Command.SendUnreliable.UnreliableSequenceNumber =
                        ENetSocket.ENET_HOST_TO_NET_16(outgoingCommand.UnreliableSequenceNumber);
                    break;
                case (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_UNSEQUENCED:
                    outgoingCommand.Command.SendUnsequenced.UnsequencedGroup =
                        ENetSocket.ENET_HOST_TO_NET_16(peer.OutgoingUnsequencedGroup);
                    break;
                default:
                    break;
            }

            if (Bitmask.IsSet(outgoingCommand.Command.Header.Command,
                (byte) ENetProtocolFlag.ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE))
            {
                peer.OutgoingReliableCommands.Add(outgoingCommand);
            }
            else
            {
                peer.OutgoingUnreliableCommands.Add(outgoingCommand);
            }
        }

        uint enet_protocol_command_size(byte commandNumber)
        {
            int commandSize =
                CommandSizes[(int) commandNumber & (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_MASK];
            return (uint) commandSize;
        }

        private ENetAcknowledgement enet_peer_queue_acknowledgement(ENetPeer peer, ENetProtocol command,
            ushort sentTime)
        {
            if (command.Header.ChannelId < peer.ChannelCount)
            {
                ENetChannel channel = peer.Channels[command.Header.ChannelId];
                ushort reliableWindow =
                    (ushort) (command.Header.ReliableSequenceNumber / ENetPeer.ENET_PEER_RELIABLE_WINDOW_SIZE);
                ushort currentWindow =
                    (ushort) (channel.IncomingReliableSequenceNumber / ENetPeer.ENET_PEER_RELIABLE_WINDOW_SIZE);

                if (command.Header.ReliableSequenceNumber < channel.IncomingReliableSequenceNumber)
                {
                    reliableWindow += (ushort) ENetPeer.ENET_PEER_RELIABLE_WINDOWS;
                }

                if (reliableWindow >= currentWindow + ENetPeer.ENET_PEER_FREE_RELIABLE_WINDOWS - 1 &&
                    reliableWindow <= currentWindow + ENetPeer.ENET_PEER_FREE_RELIABLE_WINDOWS)
                {
                    return null;
                }
            }

            ENetAcknowledgement acknowledgement = new ENetAcknowledgement();
            peer.OutgoingDataTotal += ENetProtocolAcknowledge.Size;
            acknowledgement.SentTime = sentTime;
            acknowledgement.Command = command;
            peer.Acknowledgements.Add(acknowledgement);
            return acknowledgement;
        }

        private uint RandomSeed()
        {
            using RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
            byte[] randomNumber = new byte[4];
            rng.GetBytes(randomNumber);
            uint value = BitConverter.ToUInt32(randomNumber, 0);
            return value;
        }

#if UNSAFE
        //    private unsafe T ByteArrayToStructure<T>(Span<byte> bytes) where T : struct
        //    {
        //        fixed (byte* ptr = &bytes[0])
        //        {
        //            return (T) Marshal.PtrToStructure((IntPtr) ptr, typeof(T));
        //        }
        //    }

        //    private unsafe byte[] StructureToByteArray<T>(T structure, int size) where T : struct
        //    {
        //        byte[] bytes = new byte[size];
        //        fixed (byte* ptr = &bytes[0])
        //        {
        //            Marshal.StructureToPtr(structure, (IntPtr) ptr, false);
        //        }

        //        return bytes;
        //    }

        public unsafe byte[] Serialize<T>(T value) where T : unmanaged
        {
            byte[] buffer = new byte[sizeof(T)];

            fixed (byte* bufferPtr = buffer)
            {
                Buffer.MemoryCopy(&value, bufferPtr, sizeof(T), sizeof(T));
            }

            return buffer;
        }

        public unsafe T Deserialize<T>(byte[] buffer) where T : unmanaged
        {
            T result = new T();

            fixed (byte* bufferPtr = buffer)
            {
                Buffer.MemoryCopy(bufferPtr, &result, sizeof(T), sizeof(T));
            }

            return result;
        }

        public unsafe T Deserialize<T>(Span<byte> buffer) where T : unmanaged
        {
            T result = new T();
            int len = sizeof(T);

            fixed (byte* bufferPtr = buffer)
            {
                Buffer.MemoryCopy(bufferPtr, &result, sizeof(T), sizeof(T));
            }

            return result;
        }
#else
        private T ByteArrayToStructure<T>(byte[] bytes) where T : struct
        {
            T stuff;
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            try
            {
                stuff = (T) Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }

            return stuff;
        }

        private static byte[] StructureToByteArray<T>(T data) where T : struct
        {
            byte[] rawData = new byte[Marshal.SizeOf(data)];
            GCHandle handle = GCHandle.Alloc(rawData, GCHandleType.Pinned);
            try
            {
                IntPtr rawDataPtr = handle.AddrOfPinnedObject();
                Marshal.StructureToPtr(data, rawDataPtr, false);
            }
            finally
            {
                handle.Free();
            }

            return rawData;
        }
#endif
    }
}
