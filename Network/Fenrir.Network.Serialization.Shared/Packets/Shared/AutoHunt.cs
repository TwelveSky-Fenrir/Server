using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(112)]
public readonly record struct AutoHunt : IFenrirWireType<AutoHunt>
{
    public required int BuffType { get; init; }
    [FixedArray(16)] public required int[] BuffStore { get; init; }
    public required int HuntType { get; init; }
    [FixedArray(4)] public required int[] AttackType { get; init; }
    public required int MonNum { get; init; }
    public required int ItemType { get; init; }
    public required int InvenCmd { get; init; }
    public required int DeathCmd { get; init; }
    public required int AnimalPreyCmd { get; init; }
    public required int AnimalFoodCmd { get; init; }
}
