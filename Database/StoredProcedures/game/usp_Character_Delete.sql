-- Legacy's AVATAR_INFO row carries its items/skills/hotkeys inline as packed arrays on the row itself, so
-- legacy's own DeleteCharacter (Server/ts25login/S08_MyDB.cpp:786-846) never needs a separate cleanup step
-- for them -- deleting the one row deletes everything. Fenrir normalizes those same packed arrays into
-- separate child tables (game.CharacterItems/CharacterSkills/CharacterHotkeys/CharacterBuffs/CharacterQuests/
-- CharacterFriends), every one of them FK'd back to game.Characters with NO cascade, so this procedure must
-- clean each of them up itself before the character row can be deleted.
--
-- This also severs the two self-referencing mentor-bond pointers (TeacherCharacterId/StudentCharacterId) any
-- OTHER character row may still hold pointing at this one, reproduces legacy DeleteCharacter's steps 3-5
-- (unchecked fire-and-forget cleanup of game.HeroRankings/game.OfflineShops -- the HeroRankCur/ProxyInfo
-- analogues), and additionally cleans up admin.Bans/admin.Mutes (character-only rows carry a live FK on
-- CharacterId, both independently nullable alongside AccountId) and preserves game.TribeBankLog's permanent
-- per-movement audit trail by nulling out its own now-nullable CharacterId column rather than deleting the
-- row (TribeBankLog carries no AccountId fallback, so unlike Bans/Mutes this null-out is unconditional) --
-- none of which AvatarDeletionGate's own preconditions ever check for.
--
-- Deliberately NOT cleaned up here: game.GuildMembers, game.Tribes.MasterCharacterId, game.TribeSubMasters,
-- and game.TribeVotes. Login's DeleteAvatarService (Fenrir.Application.Login.Domain.Avatars.
-- AvatarDeletionGate) already refuses the whole delete while the character holds a guild membership or a
-- tribe leadership/election role, so a live call into this procedure should never observe a row referencing
-- @CharacterId in any of those tables in the first place -- silently deleting them here would mask a
-- genuine precondition-check bug instead of surfacing it as the FK violation it should be.
CREATE PROCEDURE game.usp_Character_Delete @AccountId INT,
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
    DELETE
    FROM game.CharacterPetBag
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterRunes
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.CharacterCostumes
    WHERE CharacterId = @CharacterId;

    -- Legacy DeleteCharacter steps 3-5 (RankInfo/HeroRankCur/ProxyInfo): unchecked, fire-and-forget cleanup.
    DELETE
    FROM game.HeroRankings
    WHERE CharacterId = @CharacterId;
    DELETE
    FROM game.OfflineShops
    WHERE CharacterId = @CharacterId;

    -- admin.Bans/admin.Mutes: preserve the account-side moderation record where one exists, drop only the
    -- now-meaningless character-only rows.
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

    -- game.TribeBankLog: append-only audit trail per its own header comment -- must survive character
    -- deletion, same posture as admin.Bans/admin.Mutes above. Unlike those two, TribeBankLog carries no
    -- separate AccountId fallback column (CharacterId is its only actor reference), so the row is always
    -- preserved with CharacterId nulled out rather than branching on an account-level record still existing.
    UPDATE game.TribeBankLog
    SET CharacterId = NULL
    WHERE CharacterId = @CharacterId;

    DELETE
    FROM game.Characters
    WHERE CharacterId = @CharacterId;

    COMMIT TRANSACTION;
END;
