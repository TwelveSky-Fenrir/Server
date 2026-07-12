-- Guild-tx-hygiene fix (not legacy parity -- see the behavior contract's Part B): usp_Guild_SetGrade and
-- usp_Character_AdjustMoney used to be called as two separate round trips from GuildActionService, coordinated
-- only by an app-level try/catch compensation (set-grade-back-on-debit-failure) that is not failure-proof.
-- This procedure folds both writes into one transaction, following the same shape as
-- usp_Character_AdjustMoneyAndReplaceContainer -- no caller-side compensation is needed or possible anymore.
--
-- @CharacterId identifies whose money is debited (the guild's master); usp_Guild_SetGrade itself never took a
-- character parameter since it only ever touched game.Guilds, but the combined write needs one.
--
-- Same guarded invariants as the two single-purpose procedures it replaces for this call site:
--   - guild-not-found (50235), from usp_Guild_SetGrade (it never validated @Grade's range itself either, so
--     none is added here -- the C# service layer already derives @Grade from the guild's own current grade).
--   - the same TOCTOU-safe money cap/floor guard as usp_Character_AdjustMoney (50261 cap breach), plus a new
--     50278 for "unknown character or insufficient balance" (its own number, same precedent as 50277 above).
--
-- Also writes one game.EventLog row (Category=GuildMoney=11, EventCode=2) in the same transaction -- see
-- usp_Guild_CreateAndDebitMoney's own header for the full GL_617_GUILD_MONEY audit-logging rationale. Grade
-- logged here is the resulting (post-upgrade) grade, unlike legacy's own log call which logs the PRE-upgrade
-- grade -- deliberate divergence, since @Grade IS the value just written to game.Guilds.Grade a statement ago
-- and carrying a second, redundant "old grade" parameter would have no audit value of its own.
CREATE PROCEDURE game.usp_Guild_UpgradeAndDebitMoney @GuildId INT,
                                                     @Grade INT,
                                                     @CharacterId INT,
                                                     @DeltaMoney BIGINT,
                                                     @DeltaBigMoney INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ActorAccountId INT;
    DECLARE @AvatarName NVARCHAR(13);
    -- See usp_Guild_CreateAndDebitMoney's own comment above: EXEC cannot take CONCAT(...) inline.
    DECLARE @Payload NVARCHAR(MAX);

    BEGIN TRANSACTION;

    UPDATE game.Guilds
    SET Grade        = @Grade,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE GuildId = @GuildId;

    IF @@ROWCOUNT = 0
        THROW 50235, N'Guild not found.', 1;

    -- Guarded UPDATE closes a TOCTOU: two concurrent debits must never jointly breach the floor/cap.
    UPDATE game.Characters
    SET Money        = Money + @DeltaMoney,
        BigMoney     = BigMoney + @DeltaBigMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND Money + @DeltaMoney BETWEEN 0 AND 2000000000
      AND BigMoney + @DeltaBigMoney >= 0;

    IF @@ROWCOUNT = 0
        BEGIN
            -- Diagnostic re-read only; picks which error code to throw.
            IF EXISTS (SELECT 1
                       FROM game.Characters
                       WHERE CharacterId = @CharacterId
                         AND Money + @DeltaMoney > 2000000000)
                THROW 50261, N'Adjustment would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;

            THROW 50278, N'Unknown character or insufficient money balance for the guild upgrade cost.', 1;
        END;

    SELECT @ActorAccountId = AccountId, @AvatarName = Name
    FROM game.Characters
    WHERE CharacterId = @CharacterId;

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
