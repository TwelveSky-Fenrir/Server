-- database/50_procedures/game/usp_Guild_Create.sql
-- Contract: create a guild and enroll its master in one transaction (replaces ts25extra's
-- ZONE_GUILD_WORK_FOR_EXTRA tSort=1 SQL -- report 06 §3.1, the first of the missing guild WRITE procs).
-- Params:
--   @Name              NVARCHAR(12) -- legacy gName, the real guild identity (UQ_Guilds_Name)
--   @MasterCharacterId INT          -- becomes Guilds.MasterCharacterId AND a GuildMembers row, Role 2
-- Result set: RS0 { GuildId INT } (scalar, identity of the new guild).
-- Idempotent: no (a repeat call with the same name throws 50230).
-- The two inserts are one explicit transaction: a guild without its master member row is unreachable
-- state for every read proc (usp_GuildMember_GetByGuild-driven flows). Pre-checks THROW documented
-- errors; UQ_Guilds_Name / UQ_GuildMembers_CharacterId remain the last-resort backstops under a race
-- (same philosophy as admin.usp_Ban_Create).
-- Errors:
--   THROW 50230 -- guild name already taken (admin.ErrorCatalog, 502xx = game range).
--   THROW 50231 -- the master character already belongs to a guild.
-- [CORRIGÉ-REVUE, Phase C/V7 Guilds & Tribes] The INSERT below now sets Grade=1 explicitly. It previously
-- relied on game.Guilds' own DF_Guilds_Grade DEFAULT of 0 -- but the legacy's own CreateGuild (doc 10 §1
-- tSort 1) hard-codes `gGrade=1` on every newly created guild (S04_MyWork02.cpp's CREATE_GUILD path,
-- ts25extra), and GUILD_WORK tSort 7 (grade upgrade)'s own grade-1..4 switch has NO case for grade 0 --
-- a guild stuck at the table's raw default would therefore Quit() on every single upgrade attempt forever.
CREATE PROCEDURE game.usp_Guild_Create @Name              NVARCHAR(12),
    @MasterCharacterId INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
EXISTS (SELECT 1 FROM game.Guilds WHERE Name = @Name)
        THROW 50230, N'Guild name is already taken.', 1;

    IF
EXISTS (SELECT 1 FROM game.GuildMembers WHERE CharacterId = @MasterCharacterId)
        THROW 50231, N'Character already belongs to a guild.', 1;

    DECLARE
@GuildId INT;

BEGIN
TRANSACTION;

INSERT INTO game.Guilds (Name, MasterCharacterId, Grade)
VALUES (@Name, @MasterCharacterId, 1);

SET
@GuildId = SCOPE_IDENTITY();

INSERT INTO game.GuildMembers (GuildId, CharacterId, Role)
VALUES (@GuildId, @MasterCharacterId, 2); -- 2 = master (game.GuildMembers role enum)

COMMIT TRANSACTION;

SELECT @GuildId AS GuildId;
END;
