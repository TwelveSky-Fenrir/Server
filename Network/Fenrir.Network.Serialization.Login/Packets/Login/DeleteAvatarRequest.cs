using Fenrir.Network.Serialization.Login.Wire;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Login.Packets.Login;

/// <summary>
///     CL_DELETE_AVATAR_SEND, opcode 18. <see cref="Unknow1" /> is the legacy <c>tUnknow1</c> mode indicator (1 =
///     delete, 2 = transfer -- rejected outright, see DeleteAvatarHandler) and <see cref="Unknow2" /> is never
///     read anywhere in the cited legacy source range.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:1186-1285 ;
///     ServerDocs/11_ts25login/01_Flux_Authentification_Redirection.md:297-320 (field-to-name mapping: `r-&gt;tUnknow1`).
/// </remarks>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.DeleteAvatar, ExpectedSize = 21,
    AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly record struct DeleteAvatarRequest : IIncomingPacket<DeleteAvatarRequest>
{
    public required int AvatarPost { get; init; }
    public required int Unknow1 { get; init; }
    public required int Unknow2 { get; init; }
}
