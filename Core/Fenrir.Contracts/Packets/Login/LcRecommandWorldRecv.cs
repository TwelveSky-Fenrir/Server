using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Login;

/// <summary>
///     LC_RECOMMAND_WORLD_RECV (LOGIN.h l.216-222): emitted at the end of EVERY login train — success and
///     failure alike — right after the 3 avatar slots (SEND_LOGIN, S04_MyWork02.cpp l.63-66). The three ints are
///     locals of the legacy serializer that are never assigned, i.e. ALWAYS ZERO on the wire (login protocol
///     report §5.24 — the Phase 0 "layout unresolved" note is hereby resolved). No XOR.
/// </summary>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Outgoing, Opcodes.Login.Outgoing.RecommandWorldRecv,
    ExpectedSize = 13)]
public readonly partial record struct LcRecommandWorldRecv : IOutgoingPacket
{
    public required int AddKillOtherTribe0 { get; init; }
    public required int AddKillOtherTribe1 { get; init; }
    public required int AddKillOtherTribe2 { get; init; }
}
