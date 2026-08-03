-- Additive script: usp_Cash_Credit_UpperBoundGuard.sql stays unchanged (DbMigrator journals it by SHA-256
-- and would refuse to reapply it if edited). CREATE OR ALTER on the same procedure name, same pattern as
-- that file's own relationship to the original usp_Cash_Credit.sql.
--
-- First-credit-ever race: when @@ROWCOUNT = 0 and no game.AccountCash row exists yet for @AccountId, the
-- cap-guarded UPDATE above cannot have applied it, so the branch falls through to an unguarded
-- INSERT INTO game.AccountCash (PK_AccountCash on AccountId alone -- no bootstrap proc ever pre-creates the
-- row). Two concurrent first-ever credits for the SAME @AccountId can both pass the "no existing row" check
-- and both attempt the INSERT; the loser hits PK_AccountCash. Because that INSERT sits inside this proc's
-- own explicit BEGIN TRANSACTION, and this procedure runs entirely under XACT_ABORT ON, a losing 2627/2601
-- dooms the WHOLE transaction (XACT_STATE() = -1, verified against Microsoft Learn's TRY...CATCH/XACT_STATE
-- docs) -- a bare TRY/CATCH around just the INSERT is not sufficient, since nothing could be written again
-- in the same doomed transaction afterwards. Same ROLLBACK-and-replay idiom already used by
-- game.usp_CharacterLogoutState_Upsert and game.usp_Character_ApplyTribeFourConversion for the identical
-- shape of race: ROLLBACK TRANSACTION (the only operation a doomed transaction still permits), then a fresh
-- BEGIN TRANSACTION that re-runs the guarded UPDATE alone -- the row now exists (the winner's INSERT
-- committed before our own INSERT could raise the duplicate-key error, since SQL Server blocks a
-- conflicting insert until the other transaction resolves), so the retried UPDATE either credits this
-- caller's own @Amount on top of the winner's balance, or legitimately re-discovers the cap was hit by the
-- combined balance and THROWs 50360 again (reusing the existing error number for the same failure kind on
-- replay, matching how usp_Character_ApplyTribeFourConversion's retry reuses 50355 rather than minting a
-- new number for what is still "cap/quota exceeded", just detected one attempt later).
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

            BEGIN TRY
                INSERT INTO game.AccountCash (AccountId, Balance)
                VALUES (@AccountId, @Amount);

                INSERT INTO @Credited (BalanceAfter)
                VALUES (@Amount);
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (2627, 2601)
                    THROW;

                ROLLBACK TRANSACTION;

                BEGIN TRANSACTION;

                UPDATE game.AccountCash
                SET Balance      = Balance + @Amount,
                    UpdatedAtUtc = SYSUTCDATETIME()
                OUTPUT INSERTED.Balance
                    INTO @Credited
                WHERE AccountId = @AccountId
                  AND Balance + @Amount <= 2000000000;

                IF @@ROWCOUNT = 0
                    BEGIN
                        ROLLBACK TRANSACTION;
                        THROW 50360,
                            N'Crediting this account''s cash balance would exceed the legacy cash cap (2,000,000,000) (retry).', 1;
                    END;

                INSERT INTO game.CashLog (AccountId, Delta, BalanceAfter, Reason, ProductId)
                SELECT @AccountId, @Amount, BalanceAfter, @Reason, @ProductId
                FROM @Credited;

                COMMIT TRANSACTION;

                RETURN;
            END CATCH;
        END;

    INSERT INTO game.CashLog (AccountId, Delta, BalanceAfter, Reason, ProductId)
    SELECT @AccountId, @Amount, BalanceAfter, @Reason, @ProductId
    FROM @Credited;

    COMMIT TRANSACTION;
END;
