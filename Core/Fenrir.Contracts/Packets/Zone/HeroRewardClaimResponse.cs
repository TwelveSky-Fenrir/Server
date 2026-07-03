using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_HEROREWARD_RECV (ZONE.h:1209-1218) — builder <c>B_HEROREWARD_RECV(int tResult, void* p)</c>
///     (S05_MyTransfer.cpp:1520, field-copied from a pre-filled ZC_MULTI_ITEM_CREATE_RECV); reply to
///     CZ 119 (<c>tResult = 3</c> already claimed, S04_MyWork02.cpp:14236; <c>tResult = tNum = 1000</c>
///     success, l.14243). In EU33 the real reward is CP (<c>mUTIL.ProcessForCP</c>) — the multi-item
///     path (<c>makeMultiItem</c>) is commented out, so the struct is zero-filled except
///     <see cref="Result" />; the full layout is kept because the client decodes it whole.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.HeroRewardClaim, ExpectedSize = 57)]
public readonly partial record struct HeroRewardClaimResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Page { get; init; }
    public required int Index1 { get; init; }
    public required int Index2 { get; init; }
    public required int Xy1 { get; init; }
    public required int Xy2 { get; init; }
    [FixedArray(8)] public required int[] ItemIndex { get; init; }
}
