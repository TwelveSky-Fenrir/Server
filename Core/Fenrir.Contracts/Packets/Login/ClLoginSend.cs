using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.LoginSend, ExpectedSize = 457,
    AllowedStates = [(byte)LoginSessionState.Connected, (byte)LoginSessionState.VersionOk])]
public readonly partial record struct ClLoginSend : IIncomingPacket<ClLoginSend>
{
    [FixedString(255)] public required string Id { get; init; }
    [FixedString(33)] public required string Password { get; init; }
    public required int Version { get; init; }
    public required LoginAdapterInfo Adapter { get; init; }
}
