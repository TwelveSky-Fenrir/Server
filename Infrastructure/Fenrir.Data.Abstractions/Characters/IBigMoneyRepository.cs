namespace Fenrir.Data.Abstractions.Characters;

/// <summary>
///     CaeriusNet wrapper around the three "1B"/"1 Md" BigMoney transfer/conversion primitives
///     (CZ_PROCESS_DATA_SEND tSort 241/242/244/245/246/247) -- the only pieces of the C8-bank-store
///     container-move family not already covered by <see cref="ICharacterRepository.ReplaceContainerAsync" />/
///     <see cref="ICharacterRepository.ReplaceTwoContainersAsync" />/
///     <see cref="ICharacterRepository.AdjustStoreMoneyAsync" />/
///     <c>Fenrir.Data.Abstractions.Accounts.IAccountVaultRepository.TransferMoneyWithCharacterAsync</c> (all
///     of which the Store/Bank item and regular-money families -- tSort 223-232 -- already use end to end;
///     the item and regular-money halves needed no new repository surface at all).
///     <para>
///         A dedicated repository (not bolted onto <see cref="ICharacterRepository" /> or
///         <c>IAccountVaultRepository</c>) for the same reason <c>IRuneRepository</c>/<c>IWarPointRepository</c>
///         are dedicated: keeps this addition in new files and off either magnet interface.
///     </para>
///     <para>
///         Every method here is DELIBERATELY GENERIC (delta-in, guarded-bounds-out): none of them decide the
///         exact business magnitude of a transfer/conversion themselves -- that resolution lives entirely in
///         <c>Fenrir.Application.Game.Domain.Inventory.BigMoneyTransferPolicy</c> (241/242/244/245) and
///         <c>BigMoneyUnitConversionPolicy</c> (246/247), the domain-side sources of truth this repository's
///         procedures assume the caller has already consulted. Each method's own guarded UPDATE independently
///         re-enforces the settled ceilings (BigMoney/BigStoreMoney/vault BigMoney "Money2" &lt;= 999
///         MAX_NUMBER_SIZE2 ; Money &lt;= 2,000,000,000 MAX_NUMBER_SIZE) so a stale or racing caller can never
///         push a pool out of range no matter what it already validated in memory.
///     </para>
/// </summary>
public interface IBigMoneyRepository
{
    /// <summary>
    ///     <c>usp_Character_AdjustBigMoneyStore</c> -- tSort 241 (inventory-&gt;store)/244 (store-&gt;inventory),
    ///     a 1:1 unit transfer between game.Characters.BigMoney and game.Characters.BigStoreMoney (same row,
    ///     single guarded UPDATE, no explicit transaction needed). Throws SQL 50349 on any rejected
    ///     adjustment (unknown character, insufficient source, or either side would exceed the 999-unit cap).
    /// </summary>
    public ValueTask AdjustBigMoneyStoreAsync(int characterId, int deltaBigMoney, int deltaBigStoreMoney,
        CancellationToken ct);

    /// <summary>
    ///     <c>usp_AccountVault_TransferBigMoneyWithCharacter</c> -- tSort 242 (inventory-&gt;bank)/245
    ///     (bank-&gt;inventory), a 1:1 unit transfer between game.Characters.BigMoney (character-scoped) and
    ///     game.AccountVault.Money2 (account-scoped -- legacy masterinfo.uSaveMoney2 DB column / in-memory
    ///     wAvatar.uBigSaveMoney struct field, verified the same value via the legacy load/save binding, see
    ///     the procedure's own header). Auto-creates the AccountVault row on first use, mirroring
    ///     <c>IAccountVaultRepository.TransferMoneyWithCharacterAsync</c>'s own posture. Throws SQL 50350
    ///     (unknown character or insufficient/over-cap inventory BigMoney) or 50351 (insufficient/over-cap
    ///     vault BigMoney).
    /// </summary>
    public ValueTask AdjustBigMoneyBankAsync(int characterId, int deltaBigMoney, int accountId,
        int deltaVaultBigMoney, CancellationToken ct);

    /// <summary>
    ///     <c>usp_Character_AdjustBigMoneyConversion</c> -- the settled-ceilings half of tSort 246 (convert
    ///     wallet money up into one BigMoney unit)/247 (convert one BigMoney unit back down to wallet money).
    ///     The caller supplies <paramref name="deltaMoney" />/<paramref name="deltaBigMoney" /> already
    ///     computed per <c>BigMoneyUnitConversionPolicy</c>'s resolved legacy magnitudes (246: full requested
    ///     amount debited from Money, exactly +1 BigMoney credited -- a legacy value-loss quirk to reproduce,
    ///     not "fix" ; 247: exactly -1 BigMoney, exactly +1,000,000,000 Money regardless of the requested
    ///     count). This procedure does not itself decide that magnitude -- see the procedure's own header.
    ///     Throws SQL 50352 on any rejected adjustment.
    /// </summary>
    public ValueTask AdjustBigMoneyConversionAsync(int characterId, long deltaMoney, int deltaBigMoney,
        CancellationToken ct);
}
