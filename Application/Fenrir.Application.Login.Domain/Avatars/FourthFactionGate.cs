namespace Fenrir.Application.Login.Domain.Avatars;

/// <summary>
///     op17 CL_CREATE_AVATAR_SEND2's fourth-faction (Tribe value 3) creation exclusion -- rejects a new-avatar
///     request for the fourth playable tribe slot unless the operator has explicitly re-enabled it via
///     <see cref="LoginServerOptions.EnableFourthFaction" />. Runs only after the tribe value has already been
///     confirmed to lie in the 0-3 inclusive range by a separate, earlier check, so this gate only ever
///     observes an already-in-range value and inspects only whether it is exactly 3; tribe values 0-2 are
///     never blocked by this gate regardless of the toggle.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:635-639 (the preceding 0-3 range check this step's input is
///     already confined by) ; Server/ts25login/S04_MyWork02.cpp:640-646 (the exclusion itself, compiled only
///     under the LNW33 build macro, terminating the session on a match) ;
///     ServerDocs/11_ts25login/01_Flux_Authentification_Redirection.md:243-248 (corroborates the field-
///     validation order -- slot, name, tribe 0-3 with the LNW33 tribe-3 exclusion, head, face -- and narrates
///     640-646 as "fourth faction disabled for creation in this build").
/// </remarks>
public static class FourthFactionGate
{
    /// <summary>The fourth playable tribe slot -- Server/Header/Protocol/DEFINE.h:309 names 4 tribe slots total.</summary>
    public const byte FourthFactionTribe = 3;

    /// <summary>
    ///     True only when <paramref name="tribe" /> is exactly <see cref="FourthFactionTribe" /> and
    ///     <paramref name="enableFourthFaction" /> is false (the legacy-matching default). Tribe values 0-2 are
    ///     never blocked, regardless of the toggle.
    /// </summary>
    public static bool BlocksCreation(byte tribe, bool enableFourthFaction)
    {
        return tribe == FourthFactionTribe && !enableFourthFaction;
    }
}
