using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

/// <summary>
///     OBJECT_FOR_MONSTER (STRUCT.h:933-938, 112 bytes) — monster replication snapshot carried by
///     ZC_MONSTER_ACTION_RECV. Declared outside STRUCT.h's <c>pack(1)</c> regions but every member is
///     already 4-byte aligned (a leading <c>int</c>, the 104-byte <see cref="ActionInfo" /> block, a
///     trailing <c>int</c>) — zero padding.
/// </summary>
[FenrirWireType(112)]
public readonly partial record struct ObjectForMonster : IFenrirWireType<ObjectForMonster>
{
    public required int Index { get; init; }

    public required ActionInfo Action { get; init; }

    public required int LifeValue { get; init; }
}
