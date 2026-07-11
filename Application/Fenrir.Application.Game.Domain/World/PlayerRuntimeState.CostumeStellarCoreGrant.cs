using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     aCostumeDate[10] -- per-costume-wardrobe-slot packed enchant/combine/refine/socket word, index-aligned
    ///     with <see cref="CostumeWardrobe" /> (Server/Header/Protocol/STRUCT.h:443-445). Feeds
    ///     <see cref="Stats.StatCalculator.DecodeCostumeEnchantCs" />'s <c>cs</c> extraction -- that consumption
    ///     wiring is workstream B6's own responsibility (see that method's and
    ///     <see cref="Stats.Context.CosmeticContext.CostumeEnchantCs" />'s own remarks for the "missing
    ///     aCostumeDate source" gap this field closes); adding the source field here does not itself wire it
    ///     into <see cref="Stats.StatCalculator" />.
    /// </summary>
    /// <remarks>
    ///     Populated for the first time by the op23 costume-grant path
    ///     (workstream C9-costume-stellar-whitelist) -- every slot stays 0 until granted, same "no acquisition
    ///     path yet, real array, permanently empty until this lands" posture <see cref="CostumeWardrobe" />'s own
    ///     remarks already document for itself. No persisted column exists for this array (same as
    ///     <see cref="CostumeWardrobe" />/<see cref="StellarCoreWardrobe" /> themselves,
    ///     see <c>Fenrir.Data.Abstractions.Characters.ICharacterRepository</c>'s own "Costume/CostumeIndex... no
    ///     persisted storage exists anywhere in this schema yet" remarks) -- session-scoped only, resets to
    ///     all-zero on a fresh world entry. <b>Known, pre-existing gap, not introduced here:</b>
    ///     <see cref="ZoneTransfer.CreateEnterData" />/<see cref="PlayerEnterData" />
    ///     do NOT carry <see cref="CostumeWardrobe" />/<see cref="CostumeIndex" />/<see cref="CostumeNumber" />/
    ///     <see cref="CostumeState" />/<see cref="StellarCoreWardrobe" />/<see cref="StellarCoreIndex" />/
    ///     <see cref="StellarCoreNumber" /> through an in-process zone transfer at all today (verified: none of
    ///     those seven fields appear anywhere in <see cref="PlayerEnterData" />'s own field
    ///     list) -- this field inherits that exact same silent-reset-on-transfer gap, not a new one. Closing it
    ///     for all eight fields together is a follow-up, out of this workstream's scope.
    /// </remarks>
    public ImmutableArray<int> CostumeDate { get; set; } = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

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
