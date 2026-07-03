using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_TRIBE_BANK_RECV (ZONE.h:897-903, builder <c>B_TRIBE_BANK_RECV</c> :1107-1114) — unicast
///     response to CZ_TRIBE_BANK_SEND. <see cref="Sort" /> echoes 1 (view, <see cref="Money" />=0) or 2
///     (withdraw, <see cref="Money" /> = the player's new gold, already applied server-side).
///     <see cref="TribeBankInfo" /> holds the per-slot amounts (source of truth: ts25playuser, table
///     <c>game.TribeBank</c> in Fenrir).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TribeBank, ExpectedSize = 213)]
public readonly partial record struct TribeBankResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Sort { get; init; }
    [FixedArray(50)] public required int[] TribeBankInfo { get; init; }
    public required int Money { get; init; }
}
