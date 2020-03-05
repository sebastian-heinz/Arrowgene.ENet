ENet
===
Implementation of the ENet Protocol in C#

## Raison D'être
Looking for a C# ENet implementation yielded many projects but those where either incomplete or abandoned.
The requirements are managed code, no dll wrapper and compatible to the original C source.

## About
This project is inspired by the C implementation of (https://github.com/lsalzman/enet).
The reason being is to achieve feature compability and increase testability, as it allows to reason about the flow 
while comparing it with the original.   
However this might be suboptimal in terms of performance, the second step would be to optimize the code to take
advantage of C# features and increase readability where possible.   

## Status
- [x] enet_host_create (server = new ENetHost(address, x, x, x, x))
- [x] enet_host_service (server.Service(event, timeout))
- [ ] enet_host_create (client = new ENetHost(null, x, x, x, x))
- [ ] enet_host_connect (peer = client.Connect(address, 2, 0); )
- [ ] enet_host_service (client.Service(event, timeout))
- [x] enet_peer_send (peer.Send(channel, packet)
- [ ] enet_host_broadcast
- [ ] enet_peer_reset (peer.Reset()) (50%; `enet_peer_on_disconnect` function need to be called from peer)
- [x] ENET_PROTOCOL_COMMAND_ACKNOWLEDGE
- [x] ENET_PROTOCOL_COMMAND_CONNECT
- [ ] ENET_PROTOCOL_COMMAND_VERIFY_CONNECT
- [ ] ENET_PROTOCOL_COMMAND_DISCONNECT
- [ ] ENET_PROTOCOL_COMMAND_PING
- [x] ENET_PROTOCOL_COMMAND_SEND_RELIABLE
- [ ] ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE
- [ ] ENET_PROTOCOL_COMMAND_SEND_FRAGMENT
- [ ] ENET_PROTOCOL_COMMAND_SEND_UNSEQUENCED
- [x] ENET_PROTOCOL_COMMAND_BANDWIDTH_LIMIT
- [ ] ENET_PROTOCOL_COMMAND_THROTTLE_CONFIGURE
- [ ] ENET_PROTOCOL_COMMAND_SEND_UNRELIABLE_FRAGMENT
- [ ] move `enet_peer`-functions from ENetHost.cs -> ENetPeer.cs
- [ ] move `enet_protocol`-functions from ENetHost.cs -> ? or keep them ?
- [ ] shorten + rename `Flags` names, ex.: `ENetProtocolCommand.ENET_PROTOCOL_COMMAND_SEND_RELIABLE` -> `ENetProtocolCommand.SendReliable`
- [ ] shorten + rename functions, ex.: `ENetHost::enet_host_bandwidth_throttle` -> `ENetHost::BandwidthThrottle`
- [ ] shorten + rename constants, ex.: `ENetHost::ENET_HOST_DEFAULT_MAXIMUM_PACKET_SIZE` -> `ENetHost::DefaultMaximumPacketSize`
- [ ] surround `_logger.Debug` with `#if DEBUG / #endif`
- [ ] reduce usage of `new`-keyword / use a pool where acceptable (due to translation from c++ to C# some behaviour might be suboptimal)
- [ ] reduce coping of `byte[]` where possible, utilize `Span<T>` (due to translation from c++ to C# some behaviour might be suboptimal)
- [ ] inspect existing object pools / ensure proper use / remove unnecessary copy (due to translation from c++ to C# some behaviour might be suboptimal)
- [ ] add xml method documentation to all methods
- [ ] arrange functions / code in similar way / order as C source
- [ ] implementation of the default compressor
- [ ] implementation of the default checksum calculator
- [ ] create unit tests
- [ ] provide usage examples

## Example

### Server
```csharp
ENetHost eNetHost = new ENetHost(new IPEndPoint(IPAddress.Loopback, 10000), 32, 2, 0, 0);
ENetEvent eNetEvent = new ENetEvent();
while (true)
{
    int result = eNetHost.Service(eNetEvent, 1000);
    Console.WriteLine($"Result: {result}");
    switch (eNetEvent.Type)
    {
        case ENetEventType.ENET_EVENT_TYPE_CONNECT:
            Console.WriteLine($"A new client connected from: {eNetEvent.Peer.Address.Address}:{eNetEvent.Peer.Address.Port}");
            break;
        case ENetEventType.ENET_EVENT_TYPE_RECEIVE:
            Console.WriteLine($"A packet of length {eNetEvent.Packet.DataLength} was received from {eNetEvent.Peer.Address.Address}:{eNetEvent.Peer.Address.Port} on channel {eNetEvent.ChannelId}");
            break;
        case ENetEventType.ENET_EVENT_TYPE_DISCONNECT:
            Console.WriteLine($"Client {eNetEvent.Peer.Address.Address}:{eNetEvent.Peer.Address.Port} disconnected");
            break;
    }
}
```

## Contributors
- [@sebastian-heinz](https://github.com/sebastian-heinz)

## 3rd Parties and Libraries
- .NET Standard (https://github.com/dotnet/standard)

