namespace Fenrir.Data.Abstractions.Characters;

/// <summary>
///     Persistence for the War-Point currency (<c>game.Characters.WarPoint</c>, added by the WarPoint-currency
///     migration). War-Point is a spendable balance and is deliberately kept OUT of the write-behind progress
///     batch (<c>usp_Character_PersistProgressBatch</c>) for the same reason Money/BloodCoin are -- so a
///     last-write-wins flush can never clobber a balance. It moves only through this dedicated atomic procedure.
/// </summary>
public interface IWarPointRepository
{
    /// <summary>
    ///     Atomically debits <paramref name="warPointCost" /> War-Points and whole-replaces the destination
    ///     inventory container with <paramref name="items" />, all-or-nothing
    ///     (<c>usp_Character_BuyWarPointItem</c>). The War-Point affordability check is server-authoritative:
    ///     when the balance is insufficient nothing is changed and
    ///     <see cref="WarPointPurchaseResult.Purchased" /> is false (a soft rejection the caller must NOT treat
    ///     as a disconnect -- <c>Server/ts25zone/S04_MyWork05.cpp:1867-1873</c>). Contribution-Point cost is NOT
    ///     handled here (it is a write-behind field mirrored through the tribe-progress channel, like the
    ///     ordinary NPC-shop purchase path).
    /// </summary>
    public ValueTask<WarPointPurchaseResult> BuyWarPointItemAsync(int characterId, int warPointCost, byte container,
        IReadOnlyList<CharacterItemSlotTvp> items, CancellationToken ct);
}

/// <summary>
///     Outcome of <see cref="IWarPointRepository.BuyWarPointItemAsync" />.
///     <see cref="Purchased" /> is false when the War-Point balance was insufficient (no state changed);
///     <see cref="NewWarPointBalance" /> is meaningful only when <see cref="Purchased" /> is true.
/// </summary>
public readonly record struct WarPointPurchaseResult(bool Purchased, int NewWarPointBalance);
