using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_SKY_UP_ITEM_SEND (CLIENT.h:265-272) — typedef SHARED with <see cref="UpgradeCapeRequest" />
///     (127): identical layout, distinct C# contracts. Registered under <c>#ifdef __REBIRTH__</c>, ACTIVE
///     in EU33 (M33). "Warlord/sky" upgrade (items 87000-87128, stones 501-505, cost 50M). Response: ZC 112.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.SkyUpgradeItem, ExpectedSize = 25,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct SkyUpgradeItemRequest : IIncomingPacket<SkyUpgradeItemRequest>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }
}
