using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_TEACHER_START_RECV (ZONE.h:757-761). <c>Sort=1</c> ⇒ the attached name is your new STUDENT (you are the master,
///     recipient = CZ_TEACHER_START_SEND sender); <c>Sort=2</c> ⇒ the attached name is your new MASTER
///     (S04_MyWork02.cpp:9489-9497) — inverse of what the 02_zc_inventory pass recorded.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.MentorStart, ExpectedSize = 18)]
public readonly partial record struct MentorStartResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
}
