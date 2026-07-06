using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Domain.World.Loot;

/// <summary>
///     Everything <see cref="InventoryToWorldDropPolicy" /> (and, in principle, any other future
///     caller of the same legacy <c>MyUtil::ProcessForDropItem</c> shared spawn routine) needs handed to
///     whatever eventually turns this into a real <see cref="GroundItemEntity" /> inside a <see cref="Zone" />.
///     Deliberately does not include <see cref="GroundItemEntity.ServerIndex" />,
///     <see cref="GroundItemEntity.UniqueNumber" />, or <see cref="GroundItemEntity.CreatedAtZoneClock" /> --
///     those three are assigned by the zone at the moment of actual allocation (see
///     <see cref="Zone.SpawnGroundItem" />), never known ahead of that instant.
/// </summary>
/// <param name="Owner">
///     Maps onto <see cref="GroundItemEntity.Master" /> specifically, NOT <see cref="GroundItemEntity.PartyName" />
///     -- for a manual drop (<see cref="GroundItemEntity.ManualGroundDropSort" />), legacy's
///     <c>ProcessForDropItem</c> overwrites the ground item's <c>aMaster</c> field itself with the dropper's
///     party name when partied, rather than touching the separate <c>aPartyName</c> field monster-loot drops
///     use (see <see cref="GroundItemEntity.IsClaimableBy" />'s own remarks on this exact distinction). Already
///     resolved to "own name if unpartied, else own party's name" by the caller -- this type carries no
///     separate not-partied/partied flag.
/// </param>
public readonly record struct GroundItemSpawnPlan(
    int ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3,
    float PosX,
    float PosY,
    float PosZ,
    string Owner,
    int DropSort);

/// <summary>
///     The shared ground-item-spawn routine's (<c>MyUtil::ProcessForDropItem</c>) own gate on the item being
///     spawned -- evaluated after every other malformed-input check has already passed. Injected into
///     <see cref="InventoryToWorldDropPolicy.Resolve" /> rather than hardcoded: see that method's own
///     remarks for why the exact type whitelist / per-category value ranges are not re-derived here.
/// </summary>
public enum GroundItemSpawnEligibility
{
    Eligible,

    /// <summary>Item's coarse catalog type falls outside the small set of types the routine accepts.</summary>
    UnsupportedItemType,

    /// <summary>Item passes the type gate, but its packed per-category value is outside that category's valid encoded range.</summary>
    InvalidPackedValue
}
