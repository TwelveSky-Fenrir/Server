namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    /// <summary>
    ///     7 of the 8 legacy Valley of the Deceased (Zone 200/297/298/299) boss-win reward item ids, quantity 1
    ///     each. The 8th slot ("animal5", a randomized pet-category item) has no resolved concrete item id in the
    ///     source behavior contract this ports, so it is deliberately omitted rather than guessed -- see
    ///     <see cref="ZoneWar.ValleyWarSystem" />'s own remarks for the citation and gap note.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:11476-11532.</remarks>
    private static readonly int[] ValleyWarBossWinRewardItemIds = [1072, 1103, 1449, 1422, 1145, 2249, 602];

    /// <summary>
    ///     <see cref="ZoneCommandKind.GrantValleyWarRewardDrop" />'s handler -- the ONLY site permitted to grant
    ///     these ground-item drops, preserving the single-writer-per-zone invariant (this runs on the zone's own
    ///     tick thread; the poster, <see cref="ZoneWar.ValleyWarSystem" />, may be a DIFFERENT zone's own tick
    ///     thread when the winning-tribe member is not physically on the war map -- same cross-zone
    ///     <see cref="Post" /> precedent as <see cref="HandleRegularWarConclusionCredit" />). A no-op if the
    ///     character already left this zone, is mid zone-transfer, or is dead at drain time -- the source
    ///     contract's own eligibility gate ("currently online, not mid-zone-transfer, and not dead at this exact
    ///     tick"), rechecked here rather than trusted from the poster's own snapshot, since any of the three can
    ///     change between posting and this command draining. Every drop is granted at this character's own
    ///     CURRENT position (read live, at drain time) and attributed to their own name, matching the contract's
    ///     "granted at that player's current location" wording.
    /// </summary>
    /// <remarks>Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:11476-11532.</remarks>
    private void HandleGrantValleyWarRewardDrop(int characterId)
    {
        if (!_players.TryGetValue(characterId, out var state))
            return;

        if (state.IsMovingZone || state.IsDead)
            return;

        foreach (var itemId in ValleyWarBossWinRewardItemIds)
            SpawnGroundItem(itemId, 1, state.PosX, state.PosY, state.PosZ, state.Name, "", 0);
    }
}
