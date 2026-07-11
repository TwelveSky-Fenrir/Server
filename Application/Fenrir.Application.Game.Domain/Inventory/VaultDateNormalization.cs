namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     C1-vault-expiry-enforcement, trigger 4 (login-time normalization): once per avatar registration
///     (never once per tick), a stale <c>aInventoryDate</c> is collapsed down to the zero sentinel. This is a
///     one-way floor -- it only ever normalizes an already-past date to 0, it never extends, restores, or
///     otherwise touches a date that is still current or in the future.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/function.h:242-245 -- the shared normalization primitive: rewrites the stored
///     value only when it is strictly earlier than the supplied comparison date, otherwise leaves it
///     untouched ; Server/ts25zone/S07_MyGame03.cpp:6360-6369 -- the trigger-4 call site inside avatar
///     registration, applied to <c>aInventoryDate</c> alongside four sibling date fields (storage-vault,
///     auto-buff-time, pet-bag-date, proxy-shop-date) this contract does not otherwise cover -- see the
///     originating contract's own Side effects item 5. Only <c>aInventoryDate</c> (<see cref="World.PlayerRuntimeState.InventoryDate" />)
///     is in scope here; the four siblings are a distinct, uncovered concern and must not be silently folded
///     into this same helper.
/// </remarks>
public static class VaultDateNormalization
{
    /// <summary>
    ///     Returns the zero sentinel when <paramref name="storedDate" /> is strictly earlier than
    ///     <paramref name="today" /> (both in <see cref="Simulation.GameDate" />'s YYYYMMDD encoding);
    ///     otherwise returns <paramref name="storedDate" /> completely unchanged -- a currently-valid or
    ///     already-zero date is never modified.
    /// </summary>
    public static int NormalizeIfExpired(int storedDate, int today)
    {
        return storedDate < today ? 0 : storedDate;
    }
}
