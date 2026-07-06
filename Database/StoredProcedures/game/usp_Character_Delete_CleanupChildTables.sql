-- database/50_procedures/game/usp_Character_Delete_CleanupChildTables.sql
-- Corrective follow-up to usp_Character_Delete.sql -- that script is never edited in place once applied
-- (Fenrir.Tools.DbMigrator journals every script by content hash and refuses a changed re-apply; see its
-- own top comment).
--
-- The original body was a bare `DELETE FROM game.Characters WHERE AccountId = @AccountId AND Slot = @Slot`.
-- Legacy's AVATAR_INFO row carries its items/skills/hotkeys inline as packed arrays on the row itself, so
-- legacy's own DeleteCharacter (Server/ts25login/S08_MyDB.cpp:786-846) never needs a separate cleanup step
-- for them -- deleting the one row deletes everything. Fenrir normalizes those same packed arrays into
-- separate child tables (game.CharacterItems/CharacterSkills/CharacterHotkeys/CharacterBuffs/CharacterQuests/
-- CharacterFriends), every one of them FK'd back to game.Characters with NO cascade -- so the original bare
-- DELETE threw an FK-violation error for virtually any real character (any with even one item, skill, or
-- hotkey slot -- i.e. every character that ever entered the world) instead of ever completing successfully.
-- That left every genuine deletion attempt reporting result code 1 (database write failed) to the client,
-- never the success path the legacy contract describes.
--
-- This redefinition:
--  1) Deletes the schema-mandated child rows Fenrir's normalization requires before game.Characters itself
--     can be deleted (CharacterItems/CharacterSkills/CharacterHotkeys/CharacterBuffs/CharacterQuests/
--     CharacterFriends -- the last one in both directions, since a friend slot can name this character as
--     someone else's friend too, see game.CharacterFriends' own doc comment on one-directional rows).
--  2) Severs the two self-referencing mentor-bond pointers (TeacherCharacterId/StudentCharacterId) that ANY
--     OTHER character row may still hold pointing at this one. game.usp_CharacterMentor_ClearForCharacter
--     only clears the calling character's own pointers, not a partner's opposite pointer pointing back at
--     it (a documented legacy asymmetry for the live mentor-end flow) -- but a permanent delete cannot leave
--     that asymmetry outstanding, or FK_Characters_TeacherCharacter/FK_Characters_StudentCharacter blocks
--     the delete outright.
--  3) Reproduces legacy DeleteCharacter's steps 3-5 (Server/ts25login/S08_MyDB.cpp:786-846): unchecked
--     cleanup deletes against the character-ranking-adjacent data. game.HeroRankings is the HeroRankCur/
--     HeroRankPre analogue (ServerDocs/11_ts25login/00_OVERVIEW.md:194-203) and game.OfflineShops is the
--     ProxyInfo analogue -- both deleted unconditionally here, exactly like the legacy steps whose own
--     outcome is never checked; deleting game.OfflineShops cascades into game.OfflineShopItems
--     (FK_OfflineShopItems_Shop ... ON DELETE CASCADE), mirroring legacy's single packed-row delete. There
--     is still no Fenrir equivalent of legacy RankInfo (a general character-ranking cache distinct from
--     HeroRankCur) anywhere in this schema, so that specific legacy step has nothing to mirror yet.
--
-- Deliberately NOT cleaned up here: game.GuildMembers, game.Tribes.MasterCharacterId, game.TribeSubMasters,
-- and game.TribeVotes. Login's DeleteAvatarService (Fenrir.Application.Login.Domain.Avatars.
-- AvatarDeletionGate) already refuses the whole delete while the character holds a guild membership or a
-- tribe leadership/election role, so a live call into this procedure should never observe a row referencing
-- @CharacterId in any of those tables in the first place -- silently deleting them here would mask a
-- genuine precondition-check bug instead of surfacing it as the FK violation it should be.
CREATE
OR
ALTER PROCEDURE game.usp_Character_Delete @AccountId INT,
    @Slot TINYINT
    AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    DECLARE
@CharacterId INT = (SELECT CharacterId
                                 FROM game.Characters
                                 WHERE AccountId = @AccountId
                                   AND Slot = @Slot);

    -- Idempotent: an already-empty slot (or unknown account) affects 0 rows and raises no error (by design).
    IF
@CharacterId IS NULL
        RETURN;

BEGIN
TRANSACTION;

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

DELETE
FROM game.Characters
WHERE CharacterId = @CharacterId;

COMMIT TRANSACTION;
END;
