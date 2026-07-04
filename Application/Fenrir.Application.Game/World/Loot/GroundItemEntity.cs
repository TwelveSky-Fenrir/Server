using Fenrir.Application.Game.Simulation;

namespace Fenrir.Application.Game.World.Loot;

/// <summary>
///     One item lying on the ground in a <see cref="Zone" /> -- immutable snapshot twin of legacy
///     <c>ITEM_OBJECT</c>. Never mutated after drop; pickup is a
///     <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}" /> atomic remove, the one
///     exception to the tick-owned zone-state rule (see <see cref="Zone.TryClaimGroundItem" />).
/// </summary>
public sealed record GroundItemEntity(
    int ServerIndex,
    uint UniqueNumber,
    int ItemId,
    int Quantity,
    int Value,
    int SerialNumber,
    float PosX,
    float PosY,
    float PosZ,
    string Master,
    string PartyName,
    int DropSort,
    TimeSpan CreatedAtZoneClock,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3)
{
    public bool IsExpired(TimeSpan nowZoneClock)
    {
        return nowZoneClock - CreatedAtZoneClock >= SimulationClock.GroundItemLifetime;
    }

    /// <summary>
    ///     Ports <c>ITEM_OBJECT::CheckPossibleGetItem</c>'s ownership window: killer always owns it; anyone
    ///     can claim after <see cref="SimulationClock.GroundItemFreeForAllDelay" />; if the killer was partied
    ///     (<see cref="DropSort" /> == 1), the same party can claim after
    ///     <see cref="SimulationClock.GroundItemPartyShareDelay" />, before the free-for-all window.
    /// </summary>
    /// <remarks>
    ///     <paramref name="claimantPartyName" /> is always null today -- no party membership exists yet, so the
    ///     party-share branch never triggers, but the logic is ready for when it does.
    /// </remarks>
    public bool IsClaimableBy(string claimantName, string? claimantPartyName, TimeSpan nowZoneClock)
    {
        if (string.Equals(Master, claimantName, StringComparison.Ordinal))
            return true;

        if (IsExpired(nowZoneClock) || nowZoneClock - CreatedAtZoneClock >= SimulationClock.GroundItemFreeForAllDelay)
            return true;

        if (DropSort == 1 && !string.IsNullOrEmpty(claimantPartyName) &&
            string.Equals(PartyName, claimantPartyName, StringComparison.Ordinal) &&
            nowZoneClock - CreatedAtZoneClock >= SimulationClock.GroundItemPartyShareDelay)
            return true;

        return false;
    }
}
