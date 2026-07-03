using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_UP_LEVEL_ITEM_SEND (CLIENT.h:271) — same typedef as <see cref="SkyUpgradeItemRequest" /> (93). Cape
///     level upgrade (1401/1403/1404/1406/2208/2218/2228/2238) via stone 984/2394. Response: ZC 164.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.UpgradeCape, ExpectedSize = 25,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct UpgradeCapeRequest : IIncomingPacket<UpgradeCapeRequest>
{
    public required int Page1 { get; init; }

    public required int Index1 { get; init; }

    public required int Page2 { get; init; }

    public required int Index2 { get; init; }
}
