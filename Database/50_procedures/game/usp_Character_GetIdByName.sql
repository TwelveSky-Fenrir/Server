-- database/50_procedures/game/usp_Character_GetIdByName.sql
-- Contract: resolve an avatar NAME to its CharacterId, independent of whether that character is currently
-- online (Phase C/V7 Guilds & Tribes -- GUILD_WORK kick/AGM-promote-demote/member-title, doc 10 §1 tSort
-- 8/9/10, all operate on the guild roster via ts25extra/MySQL regardless of the target's live presence,
-- unlike the invite/transfer-leadership tSorts which require the target in the SAME zone via SearchAvatar
-- and are therefore resolved through Zone.Players/ZoneRegistry instead of this proc).
-- Params:
--   @Name NVARCHAR(13) -- exact avatar name (UQ_Characters_Name's default case-insensitive collation)
-- Result set (RS0), 0 or 1 row:
--   1. CharacterId INT
-- Idempotent: yes (read-only).
-- Errors: none (an unknown @Name simply returns zero rows).
CREATE PROCEDURE game.usp_Character_GetIdByName @Name NVARCHAR(13)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CharacterId
    FROM game.Characters
    WHERE Name = @Name;
END;
