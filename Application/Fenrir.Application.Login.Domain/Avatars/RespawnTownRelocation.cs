namespace Fenrir.Application.Login.Domain.Avatars;

/// <summary>
///     The relocation-DECISION half of <c>GetReturnBornInTownLocation</c>'s LOGIN-time call site
///     (Server/ts25login/S04_MyWork02.cpp:330-356): relocate an avatar to its own tribe's town
///     (<see cref="RespawnTownCatalog" />) when the zone it logged out in belongs to a DIFFERENT tribe than the
///     avatar's own. Net effect (A8-corridor-respawn contract, "trigger condition (login)" edge case): logging
///     out in another tribe's zone -- e.g. after dying there, or simply disconnecting there -- yanks the avatar
///     back to its own town the next time the account's character list is built.
/// </summary>
/// <remarks>
///     Deliberately NOT wired into <c>Fenrir.Application.Login.Domain.LoginTrain.BuildLogoutInfoArray</c> yet --
///     see that method's own remarks, which independently reached the same conclusion. The per-zone owning-tribe
///     table this decision needs (<c>mZoneTribeInfo</c> column 0, accessed via <c>ReturnZoneTribeInfo1</c>,
///     Server/Header/S18_MyZoneInfo.cpp:9-393/417-424 -- roughly 117-350 entries covering every zone in the
///     game, not just the sixteen corridor zones + towns <c>TribeGuardCorridorCatalogFactory</c> now covers) was
///     never recovered (A8-corridor-respawn contract, Open Question 1). <see cref="RequiresRelocation" />
///     therefore takes the owning tribe as an ALREADY-RESOLVED, definite <see cref="byte" /> rather than
///     resolving it itself or accepting an ambiguous "unknown zone" sentinel -- inventing that table (or a
///     placeholder "-1 means no owner, never relocate" guess for the sentinel) would risk silently relocating
///     every character whose logout zone has no configured owner on every single login, which is worse than
///     leaving this unwired (matching the precedent already set by
///     <c>Fenrir.Application.Login.Services.ZoneTransfer.ZoneTransferService</c>'s own deferred note for the
///     identical gap). Wire this in only once a fully-cited owning-tribe table lands.
/// </remarks>
public static class RespawnTownRelocation
{
    /// <summary>
    ///     True when the stored logout zone's own owning tribe differs from the avatar's own tribe -- the caller
    ///     must relocate the avatar to its own tribe's town (<see cref="RespawnTownCatalog.TryGetTownLocation" />)
    ///     rather than keep its raw persisted logout position.
    /// </summary>
    public static bool RequiresRelocation(byte avatarTribe, byte owningTribeOfLoggedOutZone)
    {
        return owningTribeOfLoggedOutZone != avatarTribe;
    }
}
