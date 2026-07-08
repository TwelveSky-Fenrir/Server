using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(112)]
public readonly record struct ObjectForMonster : IFenrirWireType<ObjectForMonster>
{
    public required int Index { get; init; }

    public required ActionInfo Action { get; init; }

    public required int LifeValue { get; init; }
}
