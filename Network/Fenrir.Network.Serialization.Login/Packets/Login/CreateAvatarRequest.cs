using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

/// <remarks>
/// <c>ExpectedSize = 50</c> is the 9-byte <c>CLIENT_PACKET</c> header (Server/Header/Protocol/CLIENT.h:25-31)
/// plus this struct's own 41-byte payload -- 7 four-byte ints plus a 13-byte <c>[FixedString]</c> name,
/// field-for-field matching <c>CL_CREATE_AVATAR_SEND2</c> (Server/Header/Protocol/CLIENT.h:69-79,120;
/// name length from <c>MAX_AVATAR_NAME_LENGTH</c>, Server/Header/Protocol/DEFINE.h:280). 9 + 41 = 50, not a
/// separately-unreconciled figure. The two <c>CLIENT_PACKET</c> header words ahead of the opcode byte carry
/// no size/sequence contract this decoder must honor in any shipping build: <c>tPacket2</c> has no read
/// site anywhere under Server/, and the one branch reading <c>tPacket1</c> for framing
/// (Server/ts25login/S04_MyWork01.cpp:67-71) is gated on <c>UNITY</c>, which is commented out at both of
/// its candidate definition sites (Server/Header/api.h:3, Server/Header/unity.h:1) and so never compiles;
/// every real build sources frame size from the fixed per-opcode table
/// (Server/ts25login/S04_MyWork01.cpp:66), exactly as Fenrir's own frame reader looks up frame size from a
/// server-side registry rather than the wire. Legacy also declares two structurally different packets under
/// opcode 17 -- <c>CLP_CREATE_AVATAR_SEND</c> (nested <c>AVATAR_INFO</c>) and
/// <c>CLP_CREATE_AVATAR_SEND2</c> (this flat layout), CLIENT.h:132,134 -- which one the live dispatcher
/// actually parses is resolved by the permanently-defined <c>USE_CREATE_AVATAR2</c> build flag
/// (Server/Header/Protocol/DEFINE.h:65, consulted at Server/ts25login/S04_MyWork01.cpp:17-21), which always
/// selects this flat <c>SEND2</c> layout.
/// </remarks>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.CreateAvatar,
    ExpectedSize = 50, AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct CreateAvatarRequest : IIncomingPacket<CreateAvatarRequest>
{
    public required int AvatarPost { get; init; }
    public required int Tribe { get; init; }
    public required int PreviousTribe { get; init; }
    public required int Gender { get; init; }
    public required int Head { get; init; }
    public required int Face { get; init; }
    public required int Weapon { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
}
