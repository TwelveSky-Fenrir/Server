using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_UP_LEVEL_ITEM_RECV (ZONE.h:1352-1357) — typedef SHARED with <see cref="CraftLegendaryPetResponse" />
///     (168): identical layout, distinct C# contracts. Reply to CZ_UP_LEVEL_ITEM_SEND (127), USEND
///     inline in the builder; unicast. TRAP: the trailing <c>BYTE tmp</c> is a genuine wire byte
///     (<c>#pragma pack(1)</c>, <c>sizeof</c> = 30) — total wire size is 30, not 29. It carries no
///     information (always written as 0) and cannot be modeled as <c>[Reserved(N)]</c> because nothing
///     follows it in the struct (the generator's <c>[Reserved]</c> only marks padding BEFORE a field —
///     see <see cref="ProxyStateInfo" />), so it is exposed as an explicit dead <see cref="Padding" /> byte.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.UpgradeCape, ExpectedSize = 30)]
public readonly partial record struct UpgradeCapeResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    [FixedArray(6)] public required int[] Value { get; init; }

    /// <summary>Dead trailing byte (<c>BYTE tmp</c>), always written as 0.</summary>
    public required byte Padding { get; init; }
}
