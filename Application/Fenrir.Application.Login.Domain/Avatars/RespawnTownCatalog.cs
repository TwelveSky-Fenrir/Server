namespace Fenrir.Application.Login.Domain.Avatars;

/// <summary>
///     <c>GetReturnBornInTownLocation</c>: each tribe's own respawn-town zone number and world coordinates.
///     Pure data table -- see <see cref="RespawnTownRelocation" /> for the (still partly blocked) decision of
///     WHEN a caller should apply it, and <see cref="AvatarVitalsFloor" /> for the sibling correction the same
///     legacy <c>LOGIN_SEND</c> tail segment also applies to every occupied avatar slot.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/Header/mapcheck.h:298-326 (<c>GetReturnBornInTownLocation</c> -- switch on Tribe,
///     writing the respawn zone number and the three world coordinates for tribes 0-3 ; no default case, so any
///     other tribe value is left entirely untouched by the legacy function -- see
///     <see cref="TryGetTownLocation" />'s own false-return case).
///     <c>
///         Fenrir.Application.Login.Services.
///         CreateAvatar.CreateAvatarService.SpawnMapIdByTribe
///     </c>
///     = [1, 6, 11, 140] independently corroborates the
///     four zone numbers (the same citation, read for "spawn map per Tribe" only, not the coordinates) ;
///     <c>Fenrir.Application.Game.Domain.World.ZoneWar.TribeGuardCorridorCatalogFactory</c> independently
///     corroborates the same four zone numbers as each tribe's own corridor-chain home/town zone.
///     <para>
///         The live Tribe-0 X/Z (6, -7) supersede a former, dead pair preserved only in-comment in the source
///         (X was 5, Z was 4, mapcheck.h:303,305) -- only the live values are implemented here; the reason for
///         the change and whether the former pair matters to any other system is unrecovered (A8-corridor-respawn
///         contract, Open Question 4).
///     </para>
///     <para>
///         Tribe 3's coordinates (0, 0, -6) are a deliberate asymmetric offset from that tribe's town-center
///         reference used elsewhere (e.g. its corridor-chain home zone's own default spawn point) and must be
///         preserved exactly, not "corrected" toward the other three tribes' shape (contract Edge case
///         "tribe-3 offset").
///     </para>
/// </remarks>
public static class RespawnTownCatalog
{
    private const int TribeCount = 4;

    // Index == Tribe. (ZoneId, X, Y, Z) verbatim per mapcheck.h:298-326.
    private static readonly (short ZoneId, float X, float Y, float Z)[] LocationByTribe =
    [
        (1, 6f, 0f, -7f), // Tribe 0
        (6, -190f, 0f, 1270f), // Tribe 1
        (11, 447f, 1f, 440f), // Tribe 2
        (140, 0f, 0f, -6f) // Tribe 3
    ];

    /// <summary>
    ///     True (with the tribe's own respawn zone and coordinates) for tribe 0-3; false for anything else,
    ///     matching the legacy switch's own absent default case -- an unhandled tribe leaves the caller's stored
    ///     location entirely unchanged rather than erroring or falling back to a guessed value.
    /// </summary>
    public static bool TryGetTownLocation(byte tribe, out short zoneId, out float x, out float y, out float z)
    {
        if (tribe < TribeCount)
        {
            (zoneId, x, y, z) = LocationByTribe[tribe];
            return true;
        }

        zoneId = 0;
        x = 0f;
        y = 0f;
        z = 0f;
        return false;
    }
}
