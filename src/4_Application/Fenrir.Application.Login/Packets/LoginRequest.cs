using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Login.Packets;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.Loggedin, ExpectedSize = 457,
    AllowedStates = [(byte)LoginSessionState.Connected, (byte)LoginSessionState.VersionOk])]
public readonly partial record struct LoginRequest : IIncomingPacket<LoginRequest>
{
    [FixedString(255)] public required string Id { get; init; }
    [FixedString(33)] public required string Password { get; init; }
    public required int Version { get; init; }
    public required LoginAdapterInfo Adapter { get; init; }

    public override string ToString()
    {
        return $"LoginRequest {{ Id = {Id}, Password = [REDACTED], Version = {Version}, Adapter = {Adapter} }}";
    }
}
