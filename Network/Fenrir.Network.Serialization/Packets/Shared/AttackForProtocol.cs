using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

// Reused as-is by CZ_PROCESS_ATTACK_SEND (client proposal) and ZC_PROCESS_ATTACK_RECV (server echo);
// same shape, different fill semantics for the result fields. GXCW tail member not compiled in EU33.
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
