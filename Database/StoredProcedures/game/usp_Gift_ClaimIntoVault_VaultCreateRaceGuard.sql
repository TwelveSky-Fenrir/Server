-- Additive script: usp_Gift_ClaimIntoVault.sql stays unchanged (DbMigrator journals it by SHA-256 and would
-- refuse to reapply it if edited). CREATE OR ALTER on the same procedure name, same pattern as
-- usp_AccountVault_TransferMoneyWithCharacter_VaultCreateRaceGuard.sql (that file's own header carries the
-- full race explanation this one shares).
--
-- Here the bootstrap INSERT sits between the Gift claim (game.Gifts Status 0 -> 1, captured into @Claimed)
-- and the free-slot scan/AccountVaultItems insert/GiftLog insert, so a losing 2627/2601 that dooms the whole
-- transaction (XACT_STATE() = -1) would otherwise roll the Gift claim back to Status = 0 and silently retry
-- nothing -- the caller would see a raw constraint-violation error instead of either a clean claim or a
-- documented THROW, and the gift would NOT go back to "Pending" the way a genuine 50274 full-vault failure
-- leaves it (per this procedure's own top-of-file remark). The CATCH ROLLBACK TRANSACTIONs and replays the
-- Gift claim, the free-slot scan, and both inserts in a fresh transaction, skipping the now-unnecessary
-- AccountVault existence check (the winner's INSERT is guaranteed committed by the time our own INSERT could
-- raise the duplicate-key error). @Claimed and @FreeSlot are declared once, up front, before either attempt,
-- matching game.usp_Cash_Credit_FirstCreditRaceGuard's own precedent of declaring the OUTPUT-capturing
-- variable before the guarded write so it is safely in scope for both the original path and the CATCH
-- replay; @Claimed is explicitly cleared before the replay's OUTPUT re-populates it, and @FreeSlot is
-- re-scanned fresh rather than reused, since a legitimate "vault filled up in the meantime" (THROW 50274 on
-- retry) is a real outcome here, not a bug -- same reasoning as usp_Character_ApplyTribeFourConversion's own
-- retry re-checking its quota fresh rather than trusting the failed attempt's stale read.
CREATE OR ALTER PROCEDURE game.usp_Gift_ClaimIntoVault @GiftId INT,
                                              @AccountId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE
        @Claimed TABLE
                 (
                     ProductId INT NULL,
                     Quantity  INT,
                     Value     INT
                 );

    DECLARE
        @FreeSlot SMALLINT;

    BEGIN TRANSACTION;

    UPDATE game.Gifts
    SET Status = 1
    OUTPUT INSERTED.ProductId,
           INSERTED.Quantity,
           INSERTED.Value
        INTO @Claimed
    WHERE GiftId = @GiftId
      AND AccountId = @AccountId
      AND Status = 0;

    IF
        @@ROWCOUNT = 0
        THROW 50220, N'Gift not found, not owned by this account, or already claimed.', 1;

    IF NOT EXISTS (SELECT 1 FROM game.AccountVault WHERE AccountId = @AccountId)
        BEGIN
            BEGIN TRY
                INSERT INTO game.AccountVault (AccountId) VALUES (@AccountId);
            END TRY
            BEGIN CATCH
                IF ERROR_NUMBER() NOT IN (2627, 2601)
                    THROW;

                ROLLBACK TRANSACTION;

                BEGIN TRANSACTION;

                DELETE FROM @Claimed;

                UPDATE game.Gifts
                SET Status = 1
                OUTPUT INSERTED.ProductId,
                       INSERTED.Quantity,
                       INSERTED.Value
                    INTO @Claimed
                WHERE GiftId = @GiftId
                  AND AccountId = @AccountId
                  AND Status = 0;

                IF @@ROWCOUNT = 0
                    BEGIN
                        ROLLBACK TRANSACTION;
                        THROW 50220, N'Gift not found, not owned by this account, or already claimed (retry).', 1;
                    END;

                SELECT TOP (1) @FreeSlot = Slots.n
                FROM (VALUES (0),
                             (1),
                             (2),
                             (3),
                             (4),
                             (5),
                             (6),
                             (7),
                             (8),
                             (9),
                             (10),
                             (11),
                             (12),
                             (13),
                             (14),
                             (15),
                             (16),
                             (17),
                             (18),
                             (19),
                             (20),
                             (21),
                             (22),
                             (23),
                             (24),
                             (25),
                             (26),
                             (27)) AS Slots(n)
                WHERE NOT EXISTS (SELECT 1
                                  FROM game.AccountVaultItems
                                  WHERE AccountId = @AccountId
                                    AND SlotIndex = Slots.n)
                ORDER BY Slots.n;

                IF @FreeSlot IS NULL
                    BEGIN
                        ROLLBACK TRANSACTION;
                        THROW 50274, N'Account vault is full (28 slots) (retry).', 1;
                    END;

                INSERT INTO game.AccountVaultItems (AccountId, SlotIndex, ItemId, Quantity, Value, SerialNumber, SocketData)
                SELECT @AccountId, @FreeSlot, ProductId, Quantity, Value, 0, NULL
                FROM @Claimed;

                INSERT INTO game.GiftLog (AccountId, ProductId, Quantity, Value, Status, CreatedAtUtc)
                SELECT @AccountId, ProductId, Quantity, Value, 1, SYSUTCDATETIME()
                FROM @Claimed;

                SELECT @FreeSlot AS SlotIndex;

                COMMIT TRANSACTION;

                RETURN;
            END CATCH;
        END;

    SELECT TOP (1) @FreeSlot = Slots.n
    FROM (VALUES (0),
                 (1),
                 (2),
                 (3),
                 (4),
                 (5),
                 (6),
                 (7),
                 (8),
                 (9),
                 (10),
                 (11),
                 (12),
                 (13),
                 (14),
                 (15),
                 (16),
                 (17),
                 (18),
                 (19),
                 (20),
                 (21),
                 (22),
                 (23),
                 (24),
                 (25),
                 (26),
                 (27)) AS Slots(n)
    WHERE NOT EXISTS (SELECT 1
                      FROM game.AccountVaultItems
                      WHERE AccountId = @AccountId
                        AND SlotIndex = Slots.n)
    ORDER BY Slots.n;

    IF
        @FreeSlot IS NULL
        THROW 50274, N'Account vault is full (28 slots).', 1;

    INSERT INTO game.AccountVaultItems (AccountId, SlotIndex, ItemId, Quantity, Value, SerialNumber, SocketData)
    SELECT @AccountId, @FreeSlot, ProductId, Quantity, Value, 0, NULL
    FROM @Claimed;

    INSERT INTO game.GiftLog (AccountId, ProductId, Quantity, Value, Status, CreatedAtUtc)
    SELECT @AccountId, ProductId, Quantity, Value, 1, SYSUTCDATETIME()
    FROM @Claimed;

    SELECT @FreeSlot AS SlotIndex;

    COMMIT TRANSACTION;
END;
