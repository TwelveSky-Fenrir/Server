-- Additive script: the shipped usp_Cash_Credit.sql stays unchanged (DbMigrator journals it by SHA-256 and
-- would refuse to reapply it if edited). CREATE OR ALTER on the same procedure name, same pattern as
-- usp_Character_CreateWithStarterKit_MountFix.sql.
--
-- Legacy MemberInfo.uCash is a MySQL int(11) (Server/BuildEU33/DB/nxtserver.sql:1130) credited
-- unconditionally by ts25extra with no upper-bound check at all: `UPDATE ... SET uCash=uCash+%d WHERE
-- uUserIdx=%d` (Server/ts25extra/S08_MyDB.cpp:134). This is the same unguarded-currency-mutation shape
-- CLAUDE.md documents for the EZP_30 debit hole (Server/ts25extra/S04_MyWork02.cpp:1106-1128), just on the
-- credit side instead of the debit side -- harden here rather than reproduce. Caps at the same
-- 2,000,000,000 legacy-money-cap convention already enforced elsewhere in this schema
-- (usp_Character_AdjustMoney, usp_OfflineShop_WithdrawMoney) so a credit can never push Balance to where a
-- later mutation's own INT arithmetic risks a SQL Server overflow error, and so repeated small credits
-- cannot accumulate past a sane ceiling.
--
-- Diagnostic re-read after @@ROWCOUNT = 0 picks which branch applies, same shape as
-- usp_OfflineShop_WithdrawMoney's own two-cause disambiguation: an existing row that failed the guarded
-- UPDATE means the cap was hit; no existing row means this is the account's first-ever credit, checked
-- against the same cap before the INSERT.
CREATE OR ALTER PROCEDURE game.usp_Cash_Credit @AccountId INT,
                                               @Amount INT,
                                               @Reason TINYINT,
                                               @ProductId INT = NULL
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

    COMMIT TRANSACTION;
END;
