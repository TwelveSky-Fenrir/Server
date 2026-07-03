using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_MAKE_ITEM_RECV (ZONE.h:533-537) — typedef SHARED with <see cref="CraftSkillBookResponse" /> (33):
///     identical layout, distinct C# contracts. Reply to CZ_MAKE_ITEM_SEND (29); unicast. Special result
///     codes 1001-1003 / 10001-10003 / 100000 / 100001 depending on the recipe.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.CraftItem, ExpectedSize = 29)]
public readonly partial record struct CraftItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }
}
