using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

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
