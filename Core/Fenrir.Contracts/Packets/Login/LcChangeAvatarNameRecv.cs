using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     LC_CHANGE_AVATAR_NAME_RECV (LOGIN.h l.198-202): result of a character rename (login protocol report
///     §4.19). 0 = renamed, 2 = new name equals the old one OR already taken, 3 = character is tribe leader /
///     in a guild / has friends / has a teacher or student, 101 = SQL error, 102 = update failure. No XOR.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.ChangeAvatarNameRecv,
    ExpectedSize = 5)]
public readonly partial record struct LcChangeAvatarNameRecv : IOutgoingPacket
{
    public required int Result { get; init; }
}
