using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(112)]
public readonly partial record struct ObjectForMonster : IFenrirWireType<ObjectForMonster>
{
    public required int Index { get; init; }

    public required ActionInfo Action { get; init; }

    public required int LifeValue { get; init; }
}
