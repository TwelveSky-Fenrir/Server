-- Additive script: the shipped usp_Cash_CreditAndConsumeItem.sql stays unchanged (DbMigrator journals it by
-- SHA-256 and would refuse to reapply it if edited). CREATE OR ALTER on the same procedure name, same
-- pattern as usp_Cash_Credit_UpperBoundGuard.sql (that file's own header carries the full legacy-uCash
-- citation this one shares: Server/ts25extra/S08_MyDB.cpp:134, Server/BuildEU33/DB/nxtserver.sql:1130).
--
-- This procedure is the live GP-ticket-redemption credit path (UseInventoryItemService.ResolveGpTicketAsync
-- -> ICashRepository.CreditAndConsumeItemAsync), so an unbounded Balance += @Amount here is reachable by an
-- ordinary player action repeated at will, not just an admin/webshop deposit path -- same
-- 2,000,000,000 cap and same diagnostic-re-read disambiguation as usp_Cash_Credit_UpperBoundGuard.sql,
-- reusing the same 50360 error number for the identical failure kind (mirrors how 50241 is already shared
-- across all three usp_Cash_* procedures for "amount must be positive").
CREATE OR ALTER PROCEDURE game.usp_Cash_CreditAndConsumeItem @AccountId INT,
                                                             @Amount INT,
                                                             @Reason TINYINT,
                                                             @ProductId INT = NULL,
                                                             @CharacterId INT,
                                                             @Container TINYINT,
                                                             @Items game.tvp_CharacterItemSlot READONLY
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Amount < 1
        THROW 50241, N'Cash amount must be positive.', 1;

    BEGIN TRANSACTION;

    DECLARE @Credited TABLE
                      (
                          BalanceAfter INT
                      );

    UPDATE game.AccountCash
    SET Balance      = Balance + @Amount,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT INSERTED.Balance
        INTO @Credited
    WHERE AccountId = @AccountId
      AND Balance + @Amount <= 2000000000;

    IF @@ROWCOUNT = 0
        BEGIN
            IF EXISTS (SELECT 1 FROM game.AccountCash WHERE AccountId = @AccountId)
                THROW 50360, N'Crediting this account''s cash balance would exceed the legacy cash cap (2,000,000,000).', 1;

            IF @Amount > 2000000000
                THROW 50360, N'Crediting this account''s cash balance would exceed the legacy cash cap (2,000,000,000).', 1;

            INSERT INTO game.AccountCash (AccountId, Balance)
            VALUES (@AccountId, @Amount);

            INSERT INTO @Credited (BalanceAfter)
            VALUES (@Amount);
        END;

    INSERT INTO game.CashLog (AccountId, Delta, BalanceAfter, Reason, ProductId)
    SELECT @AccountId, @Amount, BalanceAfter, @Reason, @ProductId
    FROM @Credited;

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
    FROM @Credited;

    COMMIT TRANSACTION;
END;
