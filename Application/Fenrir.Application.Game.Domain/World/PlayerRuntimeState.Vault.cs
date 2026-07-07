namespace Fenrir.Application.Game.Domain.World;

/// <summary>
///     Store/coffre (character-scoped) fields for the CZ_PROCESS_DATA_SEND Store/Save transfer families
///     (menu index 2 storage, menu index 8 vault/bank) -- <see cref="StoreMoney" />/<see cref="InventoryDate" />/
///     <see cref="StoreDate" /> already exist as persisted <c>game.Characters</c> columns and are already read
///     back by <c>CharacterWorldSnapshotDto</c>/<c>usp_Character_GetForWorldEntry</c> (and already wire-exposed
///     by <see cref="Avatars.AvatarInfoFactory" />) -- this file is the missing runtime-hydration link, same
///     posture as <see cref="PlayerRuntimeState.EatLifePotion" />'s own remarks. The account-scoped vault/bank
///     money and item pools (<c>game.AccountVault</c>/<c>AccountVaultItems</c>, legacy
///     <c>masterinfo.uSaveMoney</c>/<c>uSaveItem</c>) are deliberately NOT cached here -- every character on an
///     account shares one vault, so caching it per-character risks a stale read/write race against a different
///     character of the same account transferring concurrently; callers fetch it fresh via
///     <c>Fenrir.Data.Abstractions.Accounts.IAccountVaultRepository</c> immediately before every transfer
///     instead.
/// </summary>
public partial class PlayerRuntimeState
{
    /// <summary>
    ///     wStoreMoney -- the Store/coffre money pool, independent of <see cref="Inventory" />'s wallet money
    ///     (<c>ICharacterRepository.AdjustMoneyAsync</c>'s own <c>Money</c> column). Moved only via
    ///     <c>ICharacterRepository.AdjustStoreMoneyAsync</c> (CZ_PROCESS_DATA_SEND tSort 226/227), same
    ///     "already synchronously persisted, no dirty mark" posture as <see cref="Zone241Time" /> -- see
    ///     <see cref="Fenrir.Application.Game.Domain.World.Zone" />'s own <c>ApplyTribeProgressCommand</c>
    ///     remarks.
    /// </summary>
    public long StoreMoney { get; set; }

    /// <summary>
    ///     aInventoryDate (YYYYMMDD, <see cref="Simulation.GameDate" /> encoding) -- the second Inventory
    ///     page's purchased-extension expiry. Compared against <see cref="Simulation.GameDate.Today" /> the
    ///     same way <c>wInventoryDate &lt; ReturnNowDate()</c> gates every Store/Save deposit/withdraw that
    ///     touches Inventory page 2 (Server/Header/datetime.h:78-83, Server/ts25zone/S04_MyWork05.cpp:2559-2570
    ///     and 12 other call sites in the same file). A stale/never-purchased value (0) reads as expired,
    ///     matching legacy: 0 &lt; any real YYYYMMDD date.
    /// </summary>
    public int InventoryDate { get; set; }

    /// <summary>
    ///     aStoreDate -- same encoding/comparison as <see cref="InventoryDate" />, for the second Store page
    ///     (Server/ts25zone/S04_MyWork05.cpp:2564-2570).
    /// </summary>
    public int StoreDate { get; set; }
}
