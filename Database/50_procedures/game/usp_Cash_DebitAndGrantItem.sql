-- database/50_procedures/game/usp_Cash_DebitAndGrantItem.sql
-- Contract: atomically (1) debit an account's real-money cash balance (same guarded invariant as
-- usp_Cash_Debit, CashLog row in the SAME transaction) and (2) whole-container replace ONE of the
-- purchasing CHARACTER's item containers -- CZ_BUY_CASH_ITEM_SEND (contracts/04_commerce.md). D7 regime
-- (b): cash-shop purchase is account-scoped money crediting a character-scoped item, exactly the
-- "money AND an item container in one logical client action" case usp_Character_AdjustMoneyAndReplaceContainer's
-- own header comment calls out -- two independent round trips (usp_Cash_Debit then
-- usp_CharacterItems_ReplaceContainer) would let a fault between them take the account's cash without ever
-- granting the item.
-- Params:
--   @AccountId   INT
--   @Amount      INT        -- must be positive; the cash cost to debit
--   @Reason      TINYINT    -- game.CashLog category (item-mall purchase)
--   @ProductId   INT        -- world.ItemMallProducts.ItemMallProductId reference for the audit row
--   @CharacterId INT        -- the character (owned by @AccountId, verified app-side before calling)
--                              receiving the item
--   @Container   TINYINT
--   @Items       game.tvp_CharacterItemSlot READONLY -- the container's FULL new content
-- Result set (RS0, single row/column): NewBalance INT -- the post-debit cash balance, so the caller can
-- echo ZC_BUY_CASH_ITEM_RECV.CashSize without a second read.
-- Idempotent: no (each successful call debits @Amount and grants the item once).
-- Errors:
--   THROW 50241 -- @Amount is not positive (shared with usp_Cash_Debit/usp_Cash_Credit).
--   THROW 50240 -- insufficient cash balance (or account never credited; shared with usp_Cash_Debit).
CREATE PROCEDURE game.usp_Cash_DebitAndGrantItem @AccountId   INT,
    @Amount      INT,
    @Reason      TINYINT,
    @ProductId   INT,
    @CharacterId INT,
    @Container   TINYINT,
    @Items       game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET
NOCOUNT ON;
    SET
XACT_ABORT ON;

    IF
@Amount < 1
        THROW 50241, N'Cash amount must be positive.', 1;

BEGIN
TRANSACTION;

    DECLARE
@Debited TABLE (BalanceAfter INT);

UPDATE game.AccountCash
SET Balance      = Balance - @Amount,
    UpdatedAtUtc = SYSUTCDATETIME() OUTPUT INSERTED.Balance
INTO @Debited
WHERE AccountId = @AccountId
  AND Balance >= @Amount;

IF
@@ROWCOUNT = 0
        THROW 50240, N'Insufficient cash balance for this debit.', 1;

INSERT INTO game.CashLog (AccountId, Delta, BalanceAfter, Reason, ProductId)
SELECT @AccountId, -@Amount, BalanceAfter, @Reason, @ProductId
FROM @Debited;

DELETE
FROM game.CharacterItems
WHERE CharacterId = @CharacterId
  AND Container = @Container;

INSERT INTO game.CharacterItems (CharacterId, Container, Slot, ItemId, Quantity, Enchant, Combine,
                                 Refine, Socket, SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial)
SELECT @CharacterId,
       @Container,
       Slot,
       ItemId,
       Quantity,
       Enchant,
       Combine,
       Refine,
       Socket,
       SocketGem1,
       SocketGem2,
       SocketGem3,
       ExpireDate,
       Serial
FROM @Items;

SELECT BalanceAfter AS NewBalance
FROM @Debited;

COMMIT TRANSACTION;
END;
