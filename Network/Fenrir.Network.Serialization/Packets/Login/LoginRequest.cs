using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.Login, ExpectedSize = 457,
    AllowedStates = [(byte)LoginSessionState.Connected, (byte)LoginSessionState.VersionOk])]
public readonly partial record struct LoginRequest : IIncomingPacket<LoginRequest>
{
    [FixedString(255)] public required string Id { get; init; }
    [FixedString(33)] public required string Password { get; init; }
    public required int Version { get; init; }
    public required LoginAdapterInfo Adapter { get; init; }
}
