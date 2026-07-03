-- database/50_procedures/game/usp_Character_AdjustMoney.sql
-- Contract: atomically add (positive) or subtract (negative) a character's two money pools in one round
-- trip; rejects (does not clamp to 0) an adjustment that would drive either pool negative -- the exact
-- shape of game.usp_AccountVault_AdjustMoney applied to game.Characters.Money/BigMoney (legacy
-- aMoney/aBigMoney; D7 regime (b): money is transactional-synchronous, never write-behind).
-- Params: @CharacterId INT, @DeltaMoney BIGINT, @DeltaBigMoney INT (either may be 0 or negative).
-- Result set: none.
-- Idempotent: no (each successful call changes the balance by the given delta).
-- Single guarded UPDATE, not read-then-write: atomic under concurrent callers for the same character
-- within one statement (same idiom as the PersistBatch guard). A missing CharacterId is treated as
-- insufficient funds -- there is nothing to adjust on a character that does not exist.
-- Legacy caps (CheckOverMaximum on aMoney, billions rollover into aBigMoney) stay app-side: this proc
-- guards the invariant SQL owns (never negative -- CK_Characters_Money/BigMoney are the backstop), not
-- the wire-presentation split.
-- Errors:
--   THROW 50222 -- the adjustment would drive Money or BigMoney negative, or @CharacterId does not
--                  exist (admin.ErrorCatalog, 502xx = game range).
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
  AND Money + @DeltaMoney >= 0
  AND BigMoney + @DeltaBigMoney >= 0;

IF
@@ROWCOUNT = 0
        THROW 50222, N'Unknown character or insufficient money balance for this adjustment.', 1;
END;
