-- database/50_procedures/game/usp_Character_AdjustMoney.sql
-- Contract: atomically add (positive) or subtract (negative) a character's two money pools in one round
-- trip; rejects (does not clamp to 0) an adjustment that would drive either pool negative -- the exact
-- shape of game.usp_AccountVault_AdjustMoney applied to game.Characters.Money/BigMoney (legacy
-- aMoney/aBigMoney; D7 regime (b): money is transactional-synchronous, never write-behind).
-- Params: @CharacterId INT, @DeltaMoney BIGINT, @DeltaBigMoney INT (either may be 0 or negative).
-- Result set: none.
-- Idempotent: no (each successful call changes the balance by the given delta).
-- Single guarded UPDATE, not read-then-write: atomic under concurrent callers for the same character
-- within one statement (same idiom as the PersistBatch guard) -- BOTH the lower (>=0) and upper
-- (MAX_NUMBER_SIZE) bounds are folded into the SAME UPDATE...WHERE, not a separate pre-check: a prior pass
-- here checked the upper cap via a standalone `IF EXISTS` BEFORE the guarded UPDATE, which is a real TOCTOU
-- window -- two concurrent credits to the same character could each read the SAME pre-update Money value,
-- both pass their own cap pre-check, and then both commit, jointly pushing Money above the cap even though
-- neither delta alone would have. A missing CharacterId is treated as insufficient funds -- there is nothing
-- to adjust on a character that does not exist.
-- Legacy caps: CheckOverMaximum (Server/Header/function.h:132-140) guards every aMoney INCREASE against
-- MAX_NUMBER_SIZE = 2,000,000,000 (DEFINE.h:365) before crediting it -- verified call sites include NPC
-- shop sell (S04_MyWork05.cpp:1469/1489) and ground-money pickup (S04_MyWork05.cpp:308); a breach Quit()s
-- the player in the legacy rather than clamping. This guard now enforces that SAME cap SQL-side (single
-- source of truth for every caller of this proc, present and future) rather than leaving it purely
-- app-side commentary -- @DeltaMoney/@DeltaBigMoney vs. 0 for a DEBIT is unaffected (only an increase can
-- ever breach an upper bound). BigMoney has no equivalent cap enforced here: CheckOverBigMoneyMaximum
-- (function.h:142-150) guards a DIFFERENT, narrower quantity (the "1B" conversion pathway, tSort 240-247)
-- against MAX_NUMBER_SIZE2=999, not aBigMoney's own column value in general, and that conversion family is
-- out of this batch's scope (see ContainerMatrix's own remarks) -- documented gap, not a guess.
-- Errors:
--   THROW 50222 -- the adjustment would drive Money or BigMoney negative, or @CharacterId does not
--                  exist (admin.ErrorCatalog, 502xx = game range). Also thrown by
--                  usp_Character_AdjustMoneyAndReplaceContainer/...AndReplaceTwoContainers (same invariant).
--   THROW 50261 -- the adjustment would drive Money above MAX_NUMBER_SIZE (2,000,000,000). Also thrown by
--                  the two combined money+item procs above.
CREATE PROCEDURE game.usp_Character_AdjustMoney @CharacterId   INT,
    @DeltaMoney    BIGINT,
    @DeltaBigMoney INT
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

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
    -- The atomic guard above already decided nothing was written -- this is a purely diagnostic re-read
    -- (no TOCTOU risk: it never gates a write) to pick which of the two error codes callers rely on.
    IF EXISTS (SELECT 1
               FROM game.Characters
               WHERE CharacterId = @CharacterId
                 AND Money + @DeltaMoney > 2000000000)
        THROW 50261, N'Adjustment would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;

    THROW 50222, N'Unknown character or insufficient money balance for this adjustment.', 1;
END;
END;
