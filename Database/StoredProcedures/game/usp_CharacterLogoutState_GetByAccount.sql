-- Read every captured logout-info snapshot (game.CharacterLogoutState) for an account's characters, keyed by
-- account so the login character-select train can populate each occupied slot's LogoutInfo[6] wire array
-- (currently sent as six zeros -- Application/Fenrir.Application.Login.Domain/LoginTrain.cs). One round trip
-- for the whole (at most MAX_USER_AVATAR_NUM = 3) roster, joined through game.Characters so a caller that only
-- holds the account id never needs the character ids up front.
--
-- Returns ONLY characters that already have a captured row -- a character never yet registered into the world
-- since this feature shipped has no row, and the caller defaults its slot's LogoutInfo to all-zero (exactly
-- the pre-existing zeroed-template behavior), so the read stays additive. RCSI (READ_COMMITTED_SNAPSHOT is on
-- database-wide) means this plain SELECT takes no shared locks and never blocks the capture-side upsert.
--
-- The legacy tribe-vs-last-zone consistency correction (a last-zone whose classification does not match the
-- character's tribe resets the stored position to the character's born-in-town location -- Server/ts25login/
-- S04_MyWork02.cpp:330-356) and the life/mana low-word floor (:357-358) are DELIBERATELY not applied here:
-- both are read-side corrections whose exact inputs (the four fixed tribe-specific town-spawn value sets, and
-- the precise low-word floor bit semantics) were never enumerated by the D1 behavior contract. This procedure
-- returns the raw captured values; applying those corrections is a deferred follow-up gated on its own
-- legacy-behavior-translator pass (see LoginTrain.cs's own remarks).
CREATE PROCEDURE game.usp_CharacterLogoutState_GetByAccount @AccountId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT ls.CharacterId,
           ls.LastZone,
           ls.PosX,
           ls.PosY,
           ls.PosZ,
           ls.Life,
           ls.Mana
    FROM game.CharacterLogoutState ls
             INNER JOIN game.Characters c ON c.CharacterId = ls.CharacterId
    WHERE c.AccountId = @AccountId;
END;
