using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     ATTACK_FOR_PROTOCOL (STRUCT.h:958-978, 68 bytes) — 17 four-byte fields, no padding. The
///     <c>#ifdef GXCW int aEmTI</c> tail member is NOT compiled in EU33 (<c>GXCW</c> off). Reused as-is
///     by both CZ_PROCESS_ATTACK_SEND (client proposal) and ZC_PROCESS_ATTACK_RECV (server-recomputed
///     echo) — same wire shape, different fill semantics for the result fields.
/// </summary>
[FenrirWireType(68)]
public readonly partial record struct AttackForProtocol : IFenrirWireType<AttackForProtocol>
{
    public required int Case { get; init; }

    public required int ServerIndex1 { get; init; }

    public required uint UniqueNumber1 { get; init; }

    public required int ServerIndex2 { get; init; }

    public required uint UniqueNumber2 { get; init; }

    [FixedArray(3)] public required float[] SenderLocation { get; init; }

    public required int AttackActionValue1 { get; init; }

    public required int AttackActionValue2 { get; init; }

    public required int AttackActionValue3 { get; init; }

    public required int AttackActionValue4 { get; init; }

    public required int AttackResultValue { get; init; }

    public required int AttackCriticalExist { get; init; }

    public required int AttackElementDamage { get; init; }

    public required int AttackViewDamageValue { get; init; }

    public required int AttackRealDamageValue { get; init; }
}
