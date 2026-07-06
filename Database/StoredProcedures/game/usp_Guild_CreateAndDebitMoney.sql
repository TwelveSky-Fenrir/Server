-- Guild-tx-hygiene fix (not legacy parity -- see the behavior contract's Part B): usp_Guild_Create and
-- usp_Character_AdjustMoney used to be called as two separate round trips from GuildActionService, coordinated
-- only by an app-level try/catch compensation (disband-on-debit-failure) that is not failure-proof (a crash
-- between the two calls, or a failing compensation call itself, leaves the guild committed with no charge).
-- This procedure folds both writes into one transaction, following the same shape as
-- usp_Character_AdjustMoneyAndReplaceContainer -- no caller-side compensation is needed or possible anymore.
--
-- Same guarded invariants as the two single-purpose procedures it replaces for this call site:
--   - guild name uniqueness (50230) and "character not already in a guild" (50231), both from usp_Guild_Create.
--   - the same TOCTOU-safe money cap/floor guard as usp_Character_AdjustMoney (50261 cap breach), plus a new
--     50277 for "unknown character or insufficient balance", distinct per precedent (each *And* variant of the
--     money guard gets its own number: compare 50264/50265 on the Character container-replace variants).
CREATE PROCEDURE game.usp_Guild_CreateAndDebitMoney @Name              NVARCHAR(12),
    @MasterCharacterId INT,
    @DeltaMoney        BIGINT,
    @DeltaBigMoney     INT
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

INSERT INTO game.Guilds (Name, MasterCharacterId, Grade)
VALUES (@Name, @MasterCharacterId, 1);

SET
@GuildId = SCOPE_IDENTITY();

INSERT INTO game.GuildMembers (GuildId, CharacterId, Role)
VALUES (@GuildId, @MasterCharacterId, 2); -- 2 = master (game.GuildMembers role enum)

COMMIT TRANSACTION;

SELECT @GuildId AS GuildId;
END;
