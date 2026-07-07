namespace Fenrir.Application.Game.Domain.World.ZoneWar;

/// <summary>
///     Fixed geometry for the alliance-ceremony's two detection posts, on the map hosting legacy's
///     server-number-37 content. Unlike <see cref="HolyStoneWarSite" />'s own X/Z/radius fields (never
///     found by the translating behavior contract, so defaulted to inert zero), these are literal, cited
///     legacy world-space coordinates -- see <see cref="AlliancePostOccupantScanner" />'s own remarks.
/// </summary>
public sealed record AlliancePostSite(short MapId, float Post0X, float Post0Z, float Post1X, float Post1Z,
    float Radius);

/// <summary>
///     Resolves <see cref="AllianceDiplomacyCeremony.Tick" />'s two <see cref="AllianceCeremonyCandidate" />
///     inputs every tick by scanning a zone's currently online players for whichever tribe master ("Force
///     Leader") is standing inside each of the ceremony's two fixed posts -- the scanner
///     <see cref="AllianceDiplomacyCeremony" />'s own former GAP 1 remark asked for. Driven by
///     <c>Fenrir.Application.Game.Hosting.World.ZoneWar.AllianceDiplomacyCeremonyHost</c>, never called from
///     inside <see cref="AllianceDiplomacyCeremony" /> itself -- keeps that class's own <c>Tick</c> pure and
///     independently testable against hand-built candidates, matching its own already-extensive unit-test
///     suite.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25zone/S07_MyGame01.cpp:3781-3869 (<c>Process_Allience_Server</c>'s state-0
///     idle-scan body -- two near-identical loops over <c>mSERVER_INFO.mServerMaxUserNum</c> user slots,
///     first qualifying match wins per post via an explicit <c>break;</c>) ; Server/ts25zone/H07_MyGame.h:
///     138-139 combined with S07_MyGame01.cpp:593-600 (<c>Init()</c>'s literal assignment of both posts'
///     fixed world-space location and radius: post 0 = (-41, 8, -272) radius 10, post 1 = (35, 8, -272)
///     radius 10 -- both on the map hosting server-number-37 content) ; Server/Header/function.h:92-114
///     (<c>ReturnTribeRole</c>, 1 = tribe master/"Force Leader", the only role either post ever qualifies --
///     mirrored exactly by <see cref="PlayerRuntimeState.TribeRole" />'s own already-cited encoding).
///     <para>
///         Each post loop's qualifiers translate as: <c>!mReadyState</c> -- satisfied implicitly, a
///         character only appears in <see cref="Zone.Players" /> once fully registered/InWorld (same
///         convention already established by <c>HolyStoneWarCycle.ScanForCandidate</c> and
///         <c>Monsters.MonsterAiSystem.TryAcquireTarget</c>) ; <c>IsMovingZone()</c> -&gt;
///         <see cref="PlayerRuntimeState.IsMovingZone" /> ; <c>IsDeath()</c> -&gt;
///         <see cref="PlayerRuntimeState.IsDead" /> ; <c>ReturnTribeRole(...) != 1</c> -&gt;
///         <see cref="PlayerRuntimeState.TribeRole" /> != 1 ; <c>CheckInRange</c> (a full 3D check,
///         Server/Header/mapcheck.h:3-10) -&gt; X/Z distance only here, the same documented divergence from
///         legacy's 3D check that <c>HolyStoneWarCycle</c>'s own site checks already make (see
///         <see cref="GameServerOptions.AoiCellSize" />'s own remarks for the precedent).
///     </para>
///     <para>
///         NOT modeled: legacy's post-0-only <c>IsHiding()</c> exclusion (post 1's own loop,
///         S07_MyGame01.cpp:3828-3869, has no such check at all -- an asymmetry preserved exactly by
///         omitting the check from both posts here, not "fixed" into symmetry). No Fenrir
///         <see cref="PlayerRuntimeState" /> field models "hiding" at all yet -- an already-flagged,
///         unresolved gap elsewhere in this codebase (<c>Monsters.MonsterAiSystem</c>'s own remarks,
///         <c>HolyStoneWarCycle</c>'s own GAP (3) note), not invented here.
///     </para>
///     <para>
///         Enumeration order over <see cref="Zone.Players" /> (a dictionary) is not the same as legacy's
///         literal ascending user-slot-index order, so on the rare tick where two or more qualifying
///         masters simultaneously stand inside the SAME post's radius, which one wins "first match" can
///         differ from legacy's own slot-index tie-break. Both engines still guarantee exactly one winner
///         per post per tick either way -- a tie-break-only divergence, not a correctness gap.
///     </para>
/// </remarks>
public static class AlliancePostOccupantScanner
{
    /// <summary>
    ///     Scans <paramref name="zone" /> once for each post's current qualifying occupant. Null for a post
    ///     with no qualifying occupant this tick, matching <c>mAlliencePostAvatarIndex[n] == -1</c>.
    /// </summary>
    public static (AllianceCeremonyCandidate? PostOne, AllianceCeremonyCandidate? PostTwo) Scan(Zone zone,
        AlliancePostSite site)
    {
        var postOne = FindQualifyingLeader(zone, site.Post0X, site.Post0Z, site.Radius);
        var postTwo = FindQualifyingLeader(zone, site.Post1X, site.Post1Z, site.Radius);
        return (postOne, postTwo);
    }

    private static AllianceCeremonyCandidate? FindQualifyingLeader(Zone zone, float postX, float postZ,
        float radius)
    {
        var radiusSq = radius * radius;

        foreach (var player in zone.Players)
        {
            if (player.TribeRole != 1)
                continue;
            if (player.IsDead)
                continue;
            if (player.IsMovingZone)
                continue;
            if (DistanceSquared(player.PosX, player.PosZ, postX, postZ) > radiusSq)
                continue;

            return new AllianceCeremonyCandidate(player.CharacterId, player.Tribe);
        }

        return null;
    }

    private static float DistanceSquared(float x1, float z1, float x2, float z2)
    {
        var dx = x1 - x2;
        var dz = z1 - z2;
        return dx * dx + dz * dz;
    }
}
