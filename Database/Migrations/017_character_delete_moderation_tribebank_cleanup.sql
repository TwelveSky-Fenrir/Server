-- Corrective follow-up to StoredProcedures/game/usp_Character_Delete_CleanupChildTables.sql (never edited in
-- place -- DbMigrator journals every script by content hash and refuses a changed re-apply).
--
-- Bug: that redefinition's own header comment enumerates every table it deliberately leaves untouched
-- (game.GuildMembers, game.Tribes.MasterCharacterId, game.TribeSubMasters, game.TribeVotes) with the
-- rationale that DeleteAvatarService's AvatarDeletionGate already refuses the whole delete while any of
-- those would apply. But three more tables carry a live FOREIGN KEY on CharacterId back to game.Characters
-- that AvatarDeletionGate (Application/Fenrir.Application.Login.Domain/Avatars/AvatarDeletionGate.cs) never
-- checks at all, and that redefinition's body never cleans up either:
--   * admin.Bans / admin.Mutes: FK_Bans_Character / FK_Mutes_Character. Both CharacterId and AccountId are
--     independently NULLable (CK_Bans_AccountOrCharacter / CK_Mutes_AccountOrCharacter only require at least
--     one), and admin.usp_Ban_Create / admin.usp_Mute_Create both explicitly support a character-only ban or
--     mute (@AccountId NULL, @CharacterId supplied) -- a real, reachable moderation action, not a
--     theoretical one. Deleting a character that was ever banned or muted by CharacterId throws the exact
--     "FK violation -> client sees generic delete-failed result code 1" failure this whole corrective
--     procedure exists to eliminate for the other child tables.
--   * game.TribeBankLog: FK_TribeBankLog_Character, CharacterId NOT NULL. This is a permanent per-movement
--     audit trail (see that table's own doc comment: "closes an audit/anti-fraud gap") of every tribe-bank
--     deposit/withdrawal a character ever made, independent of the character's current tribe membership --
--     a character who made a single tribe-bank deposit years ago and later left the tribe (so none of the
--     already-guarded GuildMembers/Tribes/TribeSubMasters/TribeVotes checks apply) still cannot be deleted
--     today without an unhandled FK violation.
--
-- Fix, both following the same "unchecked, fire-and-forget cleanup" posture this procedure already applies
-- to game.HeroRankings/game.OfflineShops (neither is gated by a precondition check either):
--   * admin.Bans/admin.Mutes: null out CharacterId (preserving the row and its AccountId-side ban/mute,
--     since the account itself may still be legitimately banned/muted) when AccountId is present; delete
--     the row outright only in the character-only case (AccountId IS NULL), since a ban/mute with no
--     account association and no more character to associate it with cannot satisfy
--     CK_Bans_AccountOrCharacter/CK_Mutes_AccountOrCharacter any other way, and is meaningless once the
--     character it named can never log in again.
--   * game.TribeBankLog: delete the character's own log rows outright -- same posture as HeroRankings.
CREATE OR ALTER PROCEDURE game.usp_Character_Delete @AccountId INT,
                                                    @Slot TINYINT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @CharacterId INT = (SELECT CharacterId
                                FROM game.Characters
                                WHERE AccountId = @AccountId
                                  AND Slot = @Slot);

    -- Idempotent: an already-empty slot (or unknown account) affects 0 rows and raises no error (by design).
    IF @CharacterId IS NULL
        RETURN;

    BEGIN TRANSACTION;

    UPDATE game.Characters
    SET TeacherCharacterId = NULL
    WHERE TeacherCharacterId = @CharacterId;
    UPDATE game.Characters
    SET StudentCharacterId = NULL
    WHERE StudentCharacterId = @CharacterId;

    DELETE
    FROM game.CharacterItems
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterSkills
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterHotkeys
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterBuffs
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterQuests
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterFriends
    WHERE CharacterId = @CharacterId
       OR FriendCharacterId = @CharacterId;

    -- Legacy DeleteCharacter steps 3-5 (RankInfo/HeroRankCur/ProxyInfo): unchecked, fire-and-forget cleanup.
    DELETE
    FROM game.HeroRankings
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.OfflineShops
    WHERE CharacterId = @CharacterId;

    -- admin.Bans/admin.Mutes: preserve the account-side moderation record where one exists, drop only the
    -- now-meaningless character-only rows (see this migration's own header for the full rationale).
    UPDATE admin.Bans
    SET CharacterId = NULL
    WHERE CharacterId = @CharacterId
      AND AccountId IS NOT NULL;
    DELETE
    FROM admin.Bans
    WHERE CharacterId = @CharacterId
      AND AccountId IS NULL;

    UPDATE admin.Mutes
    SET CharacterId = NULL
    WHERE CharacterId = @CharacterId
      AND AccountId IS NOT NULL;
    DELETE
    FROM admin.Mutes
    WHERE CharacterId = @CharacterId
      AND AccountId IS NULL;

    -- game.TribeBankLog: CharacterId is NOT NULL, no account-level fallback exists -- unchecked cleanup,
    -- same posture as HeroRankings/OfflineShops above.
    DELETE
    FROM game.TribeBankLog
    WHERE CharacterId = @CharacterId;

    DELETE
    FROM game.Characters
    WHERE CharacterId = @CharacterId;

    COMMIT TRANSACTION;
END;
GO
