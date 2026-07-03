using Fenrir.Application.Game.Simulation;

namespace Fenrir.Application.Game.World.Loot;

/// <summary>
///     One item lying on the ground in a <see cref="Zone" /> -- the immutable snapshot twin of the legacy
///     <c>ITEM_OBJECT</c> (report ServerDocs/30_Fenrir_ServerLogic/05_game_mechanics.md §5, verified against
///     <c>Server/ts25zone/S07_MyGame03.cpp:426-630</c> for creation and <c>CheckPossibleGetItem</c> for
///     ownership). Immutable ON PURPOSE: every field here is fixed at drop time and never changes for the
///     item's whole lifetime -- the ONLY thing that changes is whether this instance is still present in
///     <see cref="Zone" />'s ground-item pool, which is exactly the property
///     <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}" />'s atomic
///     remove-if-still-equal primitive gives for free (see <see cref="Zone.TryClaimGroundItem" />'s remarks
///     for why pickup is the one narrow exception to the "zone state is tick-owned" rule, mirroring the same
///     reasoning already accepted for <see cref="Zone.ApplyDeath" />/<see cref="Zone.TryDamageMonster" />).
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
        return nowZoneClock - CreatedAtZoneClock >= LegacyTime.GroundItemLifetime;
    }

    /// <summary>
    ///     Ports <c>ITEM_OBJECT::CheckPossibleGetItem</c>'s ownership window (report 05 §5, DISTANCE is
    ///     checked separately by the caller -- see <see cref="Zone.TryClaimGroundItem" />): the exact killer
    ///     name always owns it; anyone owns it once <see cref="LegacyTime.GroundItemFreeForAllDelay" /> (30 s)
    ///     has passed; and when the killer was in a party at drop time (<see cref="DropSort" /> == 1), the
    ///     SAME party can also claim it after <see cref="LegacyTime.GroundItemPartyShareDelay" /> (10 s) --
    ///     BEFORE the universal free-for-all window.
    /// </summary>
    /// <remarks>
    ///     OPEN ISSUE (documented, not fabricated): Fenrir's <see cref="PlayerRuntimeState" /> has no
    ///     party/group membership field yet (a different, not-yet-built domain) -- so <paramref name="claimantPartyName" />
    ///     is always null/empty for every caller in this pass, meaning <see cref="DropSort" /> == 1's 10 s
    ///     party-share window can never actually trigger today (every monster-kill drop behaves as if the
    ///     killer were partyless). This method still implements the rule CORRECTLY and will activate the
    ///     moment party-membership plumbing exists upstream -- no changes needed here.
    /// </remarks>
    public bool IsClaimableBy(string claimantName, string? claimantPartyName, TimeSpan nowZoneClock)
    {
        if (string.Equals(Master, claimantName, StringComparison.Ordinal))
            return true;

        if (IsExpired(nowZoneClock) || nowZoneClock - CreatedAtZoneClock >= LegacyTime.GroundItemFreeForAllDelay)
            return true;

        if (DropSort == 1 && !string.IsNullOrEmpty(claimantPartyName) &&
            string.Equals(PartyName, claimantPartyName, StringComparison.Ordinal) &&
            nowZoneClock - CreatedAtZoneClock >= LegacyTime.GroundItemPartyShareDelay)
            return true;

        return false;
    }
}
