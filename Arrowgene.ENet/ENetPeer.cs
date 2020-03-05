using System.Collections.Generic;
using System.Net;
using Arrowgene.ENet.Common;
using Arrowgene.ENet.Protocol;

// ReSharper disable All

namespace Arrowgene.ENet
{
    public class ENetPeer
    {
        public const ushort ENET_PEER_DEFAULT_ROUND_TRIP_TIME = 500;
        public const ushort ENET_PEER_DEFAULT_PACKET_THROTTLE = 32;
        public const ushort ENET_PEER_PACKET_THROTTLE_SCALE = 32;
        public const ushort ENET_PEER_PACKET_THROTTLE_COUNTER = 7;
        public const ushort ENET_PEER_PACKET_THROTTLE_ACCELERATION = 2;
        public const ushort ENET_PEER_PACKET_THROTTLE_DECELERATION = 2;
        public const ushort ENET_PEER_PACKET_THROTTLE_INTERVAL = 5000;
        public const uint ENET_PEER_PACKET_LOSS_SCALE = (1 << 16);
        public const ushort ENET_PEER_PACKET_LOSS_INTERVAL = 10000;
        public const uint ENET_PEER_WINDOW_SIZE_SCALE = 64 * 1024;
        public const ushort ENET_PEER_TIMEOUT_LIMIT = 32;
        public const ushort ENET_PEER_TIMEOUT_MINIMUM = 5000;
        public const ushort ENET_PEER_TIMEOUT_MAXIMUM = 30000;
        public const ushort ENET_PEER_PING_INTERVAL = 500;
        public const ushort ENET_PEER_UNSEQUENCED_WINDOWS = 64;
        public const ushort ENET_PEER_UNSEQUENCED_WINDOW_SIZE = 1024;
        public const ushort ENET_PEER_FREE_UNSEQUENCED_WINDOWS = 32;
        public const ushort ENET_PEER_RELIABLE_WINDOWS = 16;
        public const ushort ENET_PEER_RELIABLE_WINDOW_SIZE = 0x1000;
        public const ushort ENET_PEER_FREE_RELIABLE_WINDOWS = 8;

        public ENetHost Host { get; set; }
        public ushort OutgoingPeerId { get; set; }
        public ushort IncomingPeerId { get; set; }
        public uint ConnectId { get; set; }
        public byte OutgoingSessionId { get; set; }
        public byte IncomingSessionId { get; set; }
        public IPEndPoint Address { get; set; }
        public object Data { get; set; }
        public ENetPeerState State { get; set; }
        public List<ENetChannel> Channels { get; set; }
        public uint ChannelCount { get; set; }
        public uint IncomingBandwidth { get; set; }
        public uint OutgoingBandwidth { get; set; }
        public uint IncomingBandwidthThrottleEpoch { get; set; }
        public uint OutgoingBandwidthThrottleEpoch { get; set; }
        public uint IncomingDataTotal { get; set; }
        public uint OutgoingDataTotal { get; set; }
        public uint LastSendTime { get; set; }
        public uint LastReceiveTime { get; set; }
        public uint NextTimeout { get; set; }
        public uint EarliestTimeout { get; set; }
        public uint PacketLossEpoch { get; set; }
        public uint PacketsSent { get; set; }
        public uint PacketsLost { get; set; }
        public uint PacketLoss { get; set; }
        public uint PacketLossVariance { get; set; }
        public uint PacketThrottle { get; set; }
        public uint PacketThrottleLimit { get; set; }
        public uint PacketThrottleCounter { get; set; }
        public uint PacketThrottleEpoch { get; set; }
        public uint PacketThrottleAcceleration { get; set; }
        public uint PacketThrottleDeceleration { get; set; }
        public uint PacketThrottleInterval { get; set; }
        public uint PingInterval { get; set; }
        public uint TimeoutLimit { get; set; }
        public uint TimeoutMinimum { get; set; }
        public uint TimeoutMaximum { get; set; }
        public uint LastRoundTripTime { get; set; }
        public uint LowestRoundTripTime { get; set; }
        public uint LastRoundTripTimeVariance { get; set; }
        public uint HighestRoundTripTimeVariance { get; set; }
        public uint RoundTripTime { get; set; }
        public uint RoundTripTimeVariance { get; set; }
        public uint Mtu { get; set; }
        public uint WindowSize { get; set; }
        public uint ReliableDataInTransit { get; set; }
        public ushort OutgoingReliableSequenceNumber { get; set; }
        public List<ENetAcknowledgement> Acknowledgements { get; set; }
        public List<ENetOutgoingCommand> SentReliableCommands { get; set; }
        public List<ENetOutgoingCommand> SentUnreliableCommands { get; set; }
        public List<ENetOutgoingCommand> OutgoingReliableCommands { get; set; }
        public List<ENetOutgoingCommand> OutgoingUnreliableCommands { get; set; }
        public List<ENetIncomingCommand> DispatchedCommands { get; set; }
        public bool NeedsDispatch { get; set; }
        public ushort IncomingUnsequencedGroup { get; set; }
        public ushort OutgoingUnsequencedGroup { get; set; }
        public uint[] UnsequencedWindow { get; }
        public uint EventData { get; set; }
        public uint TotalWaitingData { get; set; }

        public ENetPeer()
        {
            UnsequencedWindow = new uint [ENET_PEER_UNSEQUENCED_WINDOW_SIZE / 32];
            Acknowledgements = new List<ENetAcknowledgement>();
            SentReliableCommands = new List<ENetOutgoingCommand>();
            SentUnreliableCommands = new List<ENetOutgoingCommand>();
            OutgoingReliableCommands = new List<ENetOutgoingCommand>();
            OutgoingUnreliableCommands = new List<ENetOutgoingCommand>();
            DispatchedCommands = new List<ENetIncomingCommand>();
            Channels = new List<ENetChannel>();
        }

        public void Reset()
        {
            OutgoingPeerId = ENetHost.ENET_PROTOCOL_MAXIMUM_PEER_ID;
            ConnectId = 0;

            State = ENetPeerState.ENET_PEER_STATE_DISCONNECTED;

            IncomingBandwidth = 0;
            OutgoingBandwidth = 0;
            IncomingBandwidthThrottleEpoch = 0;
            OutgoingBandwidthThrottleEpoch = 0;
            IncomingDataTotal = 0;
            OutgoingDataTotal = 0;
            LastSendTime = 0;
            LastReceiveTime = 0;
            NextTimeout = 0;
            EarliestTimeout = 0;
            PacketLossEpoch = 0;
            PacketsSent = 0;
            PacketsLost = 0;
            PacketLoss = 0;
            PacketLossVariance = 0;
            PacketThrottle = ENET_PEER_DEFAULT_PACKET_THROTTLE;
            PacketThrottleLimit = ENET_PEER_PACKET_THROTTLE_SCALE;
            PacketThrottleCounter = 0;
            PacketThrottleEpoch = 0;
            PacketThrottleAcceleration = ENET_PEER_PACKET_THROTTLE_ACCELERATION;
            PacketThrottleDeceleration = ENET_PEER_PACKET_THROTTLE_DECELERATION;
            PacketThrottleInterval = ENET_PEER_PACKET_THROTTLE_INTERVAL;
            PingInterval = ENET_PEER_PING_INTERVAL;
            TimeoutLimit = ENET_PEER_TIMEOUT_LIMIT;
            TimeoutMinimum = ENET_PEER_TIMEOUT_MINIMUM;
            TimeoutMaximum = ENET_PEER_TIMEOUT_MAXIMUM;
            LastRoundTripTime = ENET_PEER_DEFAULT_ROUND_TRIP_TIME;
            LowestRoundTripTime = ENET_PEER_DEFAULT_ROUND_TRIP_TIME;
            LastRoundTripTimeVariance = 0;
            HighestRoundTripTimeVariance = 0;
            RoundTripTime = ENET_PEER_DEFAULT_ROUND_TRIP_TIME;
            RoundTripTimeVariance = 0;
            Mtu = Host.Mtu;
            ReliableDataInTransit = 0;
            OutgoingReliableSequenceNumber = 0;
            WindowSize = ENetHost.ENET_PROTOCOL_MAXIMUM_WINDOW_SIZE;
            IncomingUnsequencedGroup = 0;
            OutgoingUnsequencedGroup = 0;
            EventData = 0;
            TotalWaitingData = 0;

            //peer.UnsequencedWindow = new[peer.UnsequencedWindow.Length];
            ResetQueues();
        }

        public void ResetQueues()
        {
            if (NeedsDispatch)
            {
                NeedsDispatch = false;
            }

            Acknowledgements.Clear();
            SentReliableCommands.Clear();
            SentUnreliableCommands.Clear();
            OutgoingReliableCommands.Clear();
            OutgoingUnreliableCommands.Clear();
            DispatchedCommands.Clear();
            Channels.Clear();
            ChannelCount = 0;
        }

        /// <summary>
        /// Queues a packet to be sent.
        /// </summary>
        /// <param name="channelId">channel on which to send</param>
        /// <param name="packet">packet to send</param>
        /// <returns>
        /// 0 on success
        /// &lt; 0 on failure
        /// </returns>
        public int Send(byte channelId, ENetPacket packet)
        {
            ENetChannel channel = Channels[channelId];

            if (State != ENetPeerState.ENET_PEER_STATE_CONNECTED || channelId >= ChannelCount ||
                packet.DataLength > Host._maximumPacketSize)
            {
                return -1;
            }

            uint fragmentLength = Mtu - ENetProtocolHeader.Size - ENetProtocolSendFragment.Size;
            if (Host._checksum != null)
            {
                fragmentLength -= 4; //sizeof(enet_uint32);
            }

            if (packet.DataLength > fragmentLength)
            {
                uint fragmentCount = (packet.DataLength + fragmentLength - 1) / fragmentLength;
                byte commandNumber;
                ushort startSequenceNumber;

                if (fragmentCount > ENetHost.ENET_PROTOCOL_MAXIMUM_FRAGMENT_COUNT)
                {
                    return -1;
                }

                if (
                    (packet.Flags & (ENetPacketFlags.ENET_PACKET_FLAG_RELIABLE |
                                     ENetPacketFlags.ENET_PACKET_FLAG_UNRELIABLE_FRAGMENT)) ==
                    ENetPacketFlags.ENET_PACKET_FLAG_UNRELIABLE_FRAGMENT &&
                    channel.OutgoingUnreliableSequenceNumber < 0xFFFF)
                {
                    commandNumber = (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE_FRAGMENT;
                    startSequenceNumber =
                        ENetSocket.ENET_HOST_TO_NET_16((ushort) (channel.OutgoingUnreliableSequenceNumber + 1));
                }
                else
                {
                    commandNumber = (byte) ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_FRAGMENT |
                                    (byte) ENetProtocolFlag.ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE;
                    startSequenceNumber =
                        ENetSocket.ENET_HOST_TO_NET_16((ushort) (channel.OutgoingReliableSequenceNumber + 1));
                }

                List<ENetOutgoingCommand> fragments = new List<ENetOutgoingCommand>();

                ENetOutgoingCommand fragment;
                uint fragmentNumber;
                uint fragmentOffset;

                for (fragmentNumber = 0,
                    fragmentOffset = 0;
                    fragmentOffset < packet.DataLength;
                    ++fragmentNumber,
                    fragmentOffset += fragmentLength)
                {
                    if (packet.DataLength - fragmentOffset < fragmentLength)
                    {
                        fragmentLength = packet.DataLength - fragmentOffset;
                    }

                    fragment = new ENetOutgoingCommand();
                    fragment.FragmentOffset = fragmentOffset;
                    fragment.FragmentLength = (ushort) fragmentLength;
                    fragment.Packet = packet;
                    fragment.Command.Header.Command = commandNumber;
                    fragment.Command.Header.ChannelId = channelId;
                    fragment.Command.SendFragment.StartSequenceNumber = startSequenceNumber;
                    fragment.Command.SendFragment.DataLength = ENetSocket.ENET_HOST_TO_NET_16((ushort) fragmentLength);
                    fragment.Command.SendFragment.FragmentCount = ENetSocket.ENET_HOST_TO_NET_32(fragmentCount);
                    fragment.Command.SendFragment.FragmentNumber = ENetSocket.ENET_HOST_TO_NET_32(fragmentNumber);
                    fragment.Command.SendFragment.TotalLength = ENetSocket.ENET_HOST_TO_NET_32(packet.DataLength);
                    fragment.Command.SendFragment.FragmentOffset = ENetSocket.ENET_NET_TO_HOST_32(fragmentOffset);

                    fragments.Add(fragment);
                }

                packet.ReferenceCount += fragmentNumber;

                foreach (ENetOutgoingCommand currentFragment in fragments)
                {
                    Host.enet_peer_setup_outgoing_command(this, currentFragment);
                }

                return 0;
            }

            ENetProtocol command = new ENetProtocol();
            command.Header.ChannelId = channelId;

            if ((packet.Flags & (ENetPacketFlags.ENET_PACKET_FLAG_RELIABLE | ENetPacketFlags.ENET_PACKET_FLAG_UNSEQUENCED)) ==
                ENetPacketFlags.ENET_PACKET_FLAG_UNSEQUENCED)
            {
                // packetFlags.HasFlag(ENET_PACKET_FLAG_UNSEQUENCED) && !packetFlags.HasFlag(ENET_PACKET_FLAG_RELIABLE)
                command.Header.Command =
                    (byte)ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_UNSEQUENCED | (byte)ENetProtocolFlag.ENET_PROTOCOL_COMMAND_FLAG_UNSEQUENCED;
                command.SendUnsequenced.DataLength = ENetSocket.ENET_HOST_TO_NET_16((ushort)packet.DataLength);
            }
            else if ((packet.Flags & ENetPacketFlags.ENET_PACKET_FLAG_RELIABLE) != 0 || channel.OutgoingUnreliableSequenceNumber >= 0xFFFF)
            {
                // if(0) == if(false) / if(1) == if(true)
                // packetFlags.HasFlag(ENET_PACKET_FLAG_RELIABLE)
                command.Header.Command = (byte)ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_RELIABLE | (byte)ENetProtocolFlag.ENET_PROTOCOL_COMMAND_FLAG_ACKNOWLEDGE;
                command.SendReliable.DataLength = ENetSocket.ENET_HOST_TO_NET_16((ushort)packet.DataLength);
            }
            else
            {
                command.Header.Command = (byte)ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE;
                command.SendUnreliable.DataLength = ENetSocket.ENET_HOST_TO_NET_16((ushort)packet.DataLength);
            }

            if (Host.enet_peer_queue_outgoing_command(this, command, packet, 0, (ushort)packet.DataLength) == null)
            {
                return -1; 
            }
            
            return 0;
        }
    }
}
