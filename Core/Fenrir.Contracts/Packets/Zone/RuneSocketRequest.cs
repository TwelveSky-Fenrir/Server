using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_RUNE_SYSTEM_SEND (CLIENT.h:249-256) — 4 slots <c>aRuneSystem[0..3]</c>; strict item mapping
///     93514 -> slot0 ... 93517 -> slot3; <see cref="Sort" /> 0 = inventory-to-rune, 1 = rune-to-inventory;
///     any inconsistency -> Quit. NOTE: field order DIFFERS from the ZC 199 response (CZ: Sort,
///     RuneIndex, ItemIndex, Page, Index; ZC: Result, Page, Index, ItemIndex, RuneIndex) — do not copy
///     the layout across. Response: ZC 199.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.RuneSocket, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct RuneSocketRequest : IIncomingPacket<RuneSocketRequest>
{
    public required int Sort { get; init; }

    public required int RuneIndex { get; init; }

    public required int ItemIndex { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }
}
