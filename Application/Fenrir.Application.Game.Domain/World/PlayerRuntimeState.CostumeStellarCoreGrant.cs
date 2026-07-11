using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     Per-costume-wardrobe-slot expire-date entry, index-aligned with <see cref="CostumeWardrobe" />
    ///     (Server/Header/Protocol/STRUCT.h:443-445, <c>aCostumeExpireDate</c>) -- copied from the granting
    ///     item's own <see cref="Inventory.ItemStack.ExpireDate" /> at grant time
    ///     (workstream C9-costume-stellar-whitelist). Not yet consumed by any expiry-enforcement sweep -- no
    ///     such sweep exists for costume/stellar-core items today, same "real but currently inert" posture as
    ///     <see cref="CostumeDate" />.
    /// </summary>
    public ImmutableArray<int> CostumeExpireDate { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    /// <summary>
    ///     Per-stellar-core-wardrobe-slot expire-date entry, index-aligned with
    ///     <see cref="StellarCoreWardrobe" /> (Server/Header/Protocol/STRUCT.h:558-560,
    ///     <c>aStellarCoreExpireDate</c>). There is no "stellar core date" field equivalent to
    ///     <see cref="CostumeDate" /> -- the stellar-core character record carries only an item array and an
    ///     expire-date array (confirmed: STRUCT.h:558-560 declares only <c>aStellarCore</c>/
    ///     <c>aStellarCoreExpireDate</c>/<c>aStellarCoreIndex</c>, no fourth "enchant date" field). Same
    ///     session-scoped, no-persisted-column, no-expiry-sweep posture as <see cref="CostumeExpireDate" />.
    /// </summary>
    public ImmutableArray<int> StellarCoreExpireDate { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
}
