-- Guild-money-movement audit logging (create/disband/upgrade) -- engineering fix, not legacy parity. See
-- Server/ts25zone/S04_MyWork02.cpp:9985-10038 (create), :10237-10302 (disband), :10303-10418 (upgrade) and
-- Server/ts25zone/UpperCom/S06_MyUpperCom05.cpp:492-496 (GL_617_GUILD_MONEY's own
-- "(uUserIdx),(avatarName),(ACTION)(action),(MONEY)(value,grade)" format). Legacy's own version of this is a
-- lossy fire-and-forget UDP text line with a known format-string bug in the receiver -- not worth
-- reproducing byte-for-byte. Instead this closes the same audit/anti-fraud gap game.CashLog/game.TribeBankLog
-- already close for their own domains, writing into the already-existing game.EventLog under a new Category
-- (GuildMoney = 11, see Fenrir.Data.Abstractions.Game.EventLogCategory) rather than a bespoke table.
--
-- This is a behavior change to three already-applied procs, so per the "never edit an already-applied
-- script" rule these are CREATE OR ALTER (a new migration), not in-place edits of
-- StoredProcedures/game/usp_Guild_CreateAndDebitMoney.sql / usp_Guild_UpgradeAndDebitMoney.sql /
-- usp_Guild_Disband.sql. None of the three are SCHEMABINDING/natively compiled, so CREATE OR ALTER is
-- enough here -- no DROP needed first.
--
-- EventCode doubles as legacy's own action classifier for readability/continuity (1=create, 2=upgrade,
-- 3=disband -- exactly legacy's own tAction values for GL_617_GUILD_MONEY), scoped under
-- EventLogCategory.GuildMoney so it can never collide with any other category's own EventCode numbering.
-- DeltaMoney carries the signed money movement actually applied (negative for create/upgrade's debit,
-- matching CashLog/TribeBankLog's own signed-Delta convention), 0 for disband -- see the disband decision
-- below. GuildId/AvatarName/Grade have no dedicated game.EventLog columns, so they ride in Payload as
-- semicolon-separated key=value text (the same free-form convention DestroyItemService already uses for
-- its own extra context).
--
-- All three inserts happen via `EXEC game.usp_EventLog_Insert` as one more statement inside the caller's OWN
-- transaction (per that proc's own doc comment), after every guarded precondition has already passed and
-- immediately before COMMIT -- so the audit row can never survive a rolled-back mutation, and a committed
-- mutation can never be missing its audit row. Outcome=1 (success) is the only value ever logged here: every
-- failure branch THROWs before reaching the EXEC, so there is nothing to log on a rejected attempt.
--
-- Disband decision: legacy's own GL_617_GUILD_MONEY call for disband logs a real event with MONEY=0 (see
-- S04_MyWork02.cpp:10286), not a silent skip -- disband is exactly as audit-worthy as create/upgrade even
-- though no money moves (a forged/duplicated disband is still a guild-state fraud vector). This migration
-- therefore deliberately does NOT leave disband out of game.EventLog: it gets its own zero-delta row,
-- DeltaMoney explicitly 0, matching legacy's own zero-value log event. usp_Guild_Disband also gains a new
-- trailing @CharacterId parameter (appended, not inserted -- the original signature was @GuildId alone) since
-- it previously had no way to know which character was disbanding for the log's ActorCharacterId/AccountId.
CREATE
OR
ALTER PROCEDURE game.usp_Guild_CreateAndDebitMoney @Name NVARCHAR(12),
    @MasterCharacterId INT,
    @DeltaMoney BIGINT,
    @DeltaBigMoney INT
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
    DECLARE
@ActorAccountId INT;
    DECLARE
@AvatarName NVARCHAR(13);
    -- EXEC's argument list does not accept a function-call expression (CONCAT(...)) directly as a value --
    -- only a constant or a variable -- so the payload must be computed into a variable first.
    DECLARE
@Payload NVARCHAR(MAX);

BEGIN
TRANSACTION;

    -- Guarded UPDATE closes a TOCTOU: two concurrent debits must never jointly breach the floor/cap.
UPDATE game.Characters
SET Money        = Money + @DeltaMoney,
    BigMoney     = BigMoney + @DeltaBigMoney,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE CharacterId = @MasterCharacterId
  AND Money + @DeltaMoney BETWEEN 0 AND 2000000000
  AND BigMoney + @DeltaBigMoney >= 0;

IF
@@ROWCOUNT = 0
BEGIN
        -- Diagnostic re-read only; picks which error code to throw.
        IF
EXISTS (SELECT 1
                   FROM game.Characters
                   WHERE CharacterId = @MasterCharacterId
                     AND Money + @DeltaMoney > 2000000000)
            THROW 50261, N'Adjustment would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;

        THROW
50277, N'Unknown character or insufficient money balance for the guild creation cost.', 1;
END;

SELECT @ActorAccountId = AccountId, @AvatarName = Name
FROM game.Characters
WHERE CharacterId = @MasterCharacterId;

INSERT INTO game.Guilds (Name, MasterCharacterId, Grade)
VALUES (@Name, @MasterCharacterId, 1);

SET
@GuildId = SCOPE_IDENTITY();

INSERT INTO game.GuildMembers (GuildId, CharacterId, Role)
VALUES (@GuildId, @MasterCharacterId, 2); -- 2 = master (game.GuildMembers role enum)

SET @Payload = CONCAT(N'GuildId=', @GuildId, N';AvatarName=', @AvatarName, N';Grade=1');

EXEC game.usp_EventLog_Insert
        @EventCode = 1, -- create (legacy GL_617_GUILD_MONEY tAction=1)
        @Category = 11, -- game.EventLogCategory.GuildMoney
        @ActorAccountId = @ActorAccountId,
        @ActorCharacterId = @MasterCharacterId,
        @DeltaMoney = @DeltaMoney,
        @DeltaBigMoney = @DeltaBigMoney,
        @Outcome = 1,
        @Payload = @Payload;

COMMIT TRANSACTION;

SELECT @GuildId AS GuildId;
END;
GO

CREATE
OR
ALTER PROCEDURE game.usp_Guild_UpgradeAndDebitMoney @GuildId INT,
    @Grade INT,
    @CharacterId INT,
    @DeltaMoney BIGINT,
    @DeltaBigMoney INT
    AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    DECLARE
@ActorAccountId INT;
    DECLARE
@AvatarName NVARCHAR(13);
    -- See usp_Guild_CreateAndDebitMoney's own comment above: EXEC cannot take CONCAT(...) inline.
    DECLARE
@Payload NVARCHAR(MAX);

BEGIN
TRANSACTION;

UPDATE game.Guilds
SET Grade = @Grade
WHERE GuildId = @GuildId;

IF
@@ROWCOUNT = 0
        THROW 50235, N'Guild not found.', 1;

    -- Guarded UPDATE closes a TOCTOU: two concurrent debits must never jointly breach the floor/cap.
UPDATE game.Characters
SET Money        = Money + @DeltaMoney,
    BigMoney     = BigMoney + @DeltaBigMoney,
    UpdatedAtUtc = SYSUTCDATETIME()
WHERE CharacterId = @CharacterId
  AND Money + @DeltaMoney BETWEEN 0 AND 2000000000
  AND BigMoney + @DeltaBigMoney >= 0;

IF
@@ROWCOUNT = 0
BEGIN
        -- Diagnostic re-read only; picks which error code to throw.
        IF
EXISTS (SELECT 1
                   FROM game.Characters
                   WHERE CharacterId = @CharacterId
                     AND Money + @DeltaMoney > 2000000000)
            THROW 50261, N'Adjustment would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;

        THROW
50278, N'Unknown character or insufficient money balance for the guild upgrade cost.', 1;
END;

SELECT @ActorAccountId = AccountId, @AvatarName = Name
FROM game.Characters
WHERE CharacterId = @CharacterId;

-- Grade logged here is the resulting (post-upgrade) grade, unlike legacy's own log call which logs the
-- PRE-upgrade grade (mEXTRA.mRecv_GuildInfo.gGrade, captured before the round trip that performs the
-- actual upgrade -- see S04_MyWork02.cpp:10412, which never re-fetches gGrade after the upgrade lands).
-- Deliberate divergence: this proc's @Grade parameter IS the value just written to game.Guilds.Grade a
-- statement ago, so logging anything else would mean carrying a second, redundant "old grade" parameter
-- purely to reproduce a legacy quirk with no audit value of its own.
SET @Payload = CONCAT(N'GuildId=', @GuildId, N';AvatarName=', @AvatarName, N';Grade=', @Grade);

EXEC game.usp_EventLog_Insert
        @EventCode = 2, -- upgrade (legacy GL_617_GUILD_MONEY tAction=2)
        @Category = 11, -- game.EventLogCategory.GuildMoney
        @ActorAccountId = @ActorAccountId,
        @ActorCharacterId = @CharacterId,
        @DeltaMoney = @DeltaMoney,
        @DeltaBigMoney = @DeltaBigMoney,
        @Outcome = 1,
        @Payload = @Payload;

COMMIT TRANSACTION;
END;
GO

CREATE
OR
ALTER PROCEDURE game.usp_Guild_Disband @GuildId INT, @CharacterId INT
    AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    DECLARE
@Grade INT;
    DECLARE
@ActorAccountId INT;
    DECLARE
@AvatarName NVARCHAR(13);
    -- See usp_Guild_CreateAndDebitMoney's own comment above: EXEC cannot take CONCAT(...) inline.
    DECLARE
@Payload NVARCHAR(MAX);

BEGIN
TRANSACTION;

    -- Read the guild's grade and the acting character's identity BEFORE the deletes below remove the guild
    -- row -- there is nothing left to join against afterward. Neither SELECT has a side effect, so doing
    -- them before the not-found check below is safe: if @GuildId doesn't exist, @Grade stays NULL and the
    -- deletes' final @@ROWCOUNT = 0 still throws 50235 before the EXEC is ever reached.
SELECT @Grade = Grade
FROM game.Guilds
WHERE GuildId = @GuildId;

SELECT @ActorAccountId = AccountId, @AvatarName = Name
FROM game.Characters
WHERE CharacterId = @CharacterId;

-- game.GuildNotices access requires WITH (SNAPSHOT): the database does not set
-- MEMORY_OPTIMIZED_ELEVATE_TO_SNAPSHOT, so the hint is mandatory here, not defensive.
DELETE
FROM game.GuildNotices WITH (SNAPSHOT)
WHERE GuildId = @GuildId;

DELETE
FROM game.GuildMembers
WHERE GuildId = @GuildId;

DELETE
FROM game.Guilds
WHERE GuildId = @GuildId;

IF
@@ROWCOUNT = 0
        THROW 50235, N'Guild not found.', 1;

    -- Disband decision (see this migration's own header comment): logged with DeltaMoney=0, matching
    -- legacy's own zero-value GL_617_GUILD_MONEY(..., 0, 3, ...) call for this branch -- disband is still an
    -- audit-worthy guild-state change even though no money moves, so it is not left out of game.EventLog.
SET @Payload = CONCAT(N'GuildId=', @GuildId, N';AvatarName=', @AvatarName, N';Grade=', @Grade);

EXEC game.usp_EventLog_Insert
        @EventCode = 3, -- disband (legacy GL_617_GUILD_MONEY tAction=3)
        @Category = 11, -- game.EventLogCategory.GuildMoney
        @ActorAccountId = @ActorAccountId,
        @ActorCharacterId = @CharacterId,
        @DeltaMoney = 0,
        @Outcome = 1,
        @Payload = @Payload;

COMMIT TRANSACTION;
END;
GO
