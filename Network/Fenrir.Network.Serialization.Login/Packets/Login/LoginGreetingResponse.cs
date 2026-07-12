using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

/// <remarks>
/// Legacy's <c>LC_LOGIN_CONNECT_OK_RECV</c> (Server/Header/Protocol/LOGIN.h:98-109) declares five reserved
/// ints (<c>tPad0</c>..<c>tPad4</c>) ahead of <c>tRandomNumber</c>, but <c>B_LOGIN_CONNECT_OK_RECV</c>
/// (Server/ts25login/S05_MyTransfer.cpp:34-52) only ever assigns the first four -- the last four bytes are
/// whatever a previously-built packet left in the single process-wide transfer buffer
/// (Server/ts25login/S05_MyTransfer.cpp:20-31), never cleared or reallocated between sends. Zeroing all 20
/// reserved bytes here, every send, is a deliberate choice not to reproduce that stale-heap information
/// leak -- nothing in the legacy contract obligates it, and the wire size (37 bytes) matches exactly either
/// way.
/// </remarks>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.LoginGreeting,
    Obfuscation = WireObfuscationMode.XorPacketGlobal, ExpectedSize = 37)]
public readonly partial record struct LoginGreetingResponse : IOutgoingPacket
{
    [Reserved(20)] public required int RandomNumber { get; init; }
    public required int MaxPlayerNum { get; init; }
    public required int GagePlayerNum { get; init; }
    public required int PresentPlayerNum { get; init; }
}
