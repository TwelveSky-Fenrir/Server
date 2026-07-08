using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.Loggedin, ExpectedSize = 457,
    AllowedStates = [(byte)LoginSessionState.Connected, (byte)LoginSessionState.VersionOk])]
public readonly record struct LoginRequest : IIncomingPacket<LoginRequest>
{
    [FixedString(255)] public required string Id { get; init; }
    [FixedString(33)] public required string Password { get; init; }
    public required int Version { get; init; }
    public required LoginAdapterInfo Adapter { get; init; }

    /// <summary>
    ///     Overrides the record's default member-by-member ToString() so this type can never print the
    ///     plaintext <see cref="Password" /> it still carries for legacy-client compatibility -- see finding
    ///     4, ServerDocs/11_ts25login/03_Plaintext_Passwords_Secrets.md. No current call site logs this packet
    ///     as a whole object today (LoginService/LoginHandler only ever log <see cref="Id" />, never the
    ///     packet itself), but nothing about the type prevented a future "{Packet}"-style structured log or
    ///     debugger watch from doing so before this override existed.
    /// </summary>
    public override string ToString()
    {
        return $"LoginRequest {{ Id = {Id}, Password = [REDACTED], Version = {Version}, Adapter = {Adapter} }}";
    }
}
