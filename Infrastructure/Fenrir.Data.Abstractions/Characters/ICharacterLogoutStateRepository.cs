using System.Collections.Immutable;

namespace Fenrir.Data.Abstractions.Characters;

/// <summary>
///     CaeriusNet wrapper around <c>game.CharacterLogoutState</c> -- the durable per-character "logout info"
///     snapshot (zone/position/life/mana) that closes the D1 finding "UPDATE_LOGOUT_INFO[6] is never
///     populated/restored on reconnect". A distinct, capture-time snapshot store: it must never become a
///     second authority for the live world spawn (<c>game.Characters</c> already owns live placement via the
///     write-behind flush) -- see <c>Database/Tables/game/CharacterLogoutState.sql</c>'s own header.
/// </summary>
public interface ICharacterLogoutStateRepository
{
    /// <summary>
    ///     <c>usp_CharacterLogoutState_Upsert</c>: captures (or re-anchors) one character's logout-info
    ///     snapshot. Called from <c>EnterWorldService</c> at successful world entry, re-stamping the snapshot
    ///     to <c>game.Characters</c>' just-loaded authoritative placement each session.
    /// </summary>
    /// <remarks>
    ///     This is the enter-world capture point only. The legacy continuous in-session re-capture (on
    ///     movement/teleport, a ~2-minute periodic snapshot, and on quit -- Server/ts25zone/S04_MyWork02.cpp:
    ///     1778,1920, ZoneWorker.cpp:53-62, S03_MyUser.cpp:386) is a deferred follow-up: it belongs in the
    ///     write-behind flush / disconnect hook (owned by the zone tick, not this enter-world path).
    /// </remarks>
    public ValueTask UpsertAsync(int characterId, int lastZone, int posX, int posY, int posZ, int life, int mana,
        CancellationToken ct);

    /// <summary>
    ///     <c>usp_CharacterLogoutState_GetByAccount</c>: returns the captured snapshot for every one of an
    ///     account's characters that already has one (a character never yet registered into the world has no
    ///     row -- the caller defaults its slot's <c>LogoutInfo</c> to all-zero). One round trip for the whole
    ///     (at most 3-slot) roster, used to populate the login character-select train's <c>LogoutInfo</c> wire
    ///     array. The raw captured values are returned; the tribe-vs-last-zone correction and life/mana floor
    ///     are deferred (contract-blocked -- see the procedure's own header).
    /// </summary>
    public ValueTask<ImmutableArray<CharacterLogoutStateDto>> GetByAccountAsync(int accountId, CancellationToken ct);
}
