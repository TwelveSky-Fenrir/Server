-- database/50_procedures/game/usp_TribeBank_DepositFromCharacter.sql
-- The character-facing mirror of usp_TribeBank_Withdraw: moves a character's entire current Money (never a
-- partial amount, matching the withdraw side's own "never a partial withdrawal" convention -- CZ_TRIBE_BANK_SEND
-- has no separate amount field to request a lesser sum) into one tribe-bank slot. Gives
-- game.usp_TribeBank_Deposit its first real caller: that proc already existed for the legacy PlayUser
-- process's periodic system-side tax sweep (ZONE_TRIBE_BANK_SAVE_FOR_PLAYUSER_SEND), which required a
-- WorldState/territory-ownership model that Fenrir has not ported yet (tracked separately); this proc instead
-- exposes the same underlying "add money to a tribe-bank slot" capability as a safe, player-initiated donation
-- of the player's own money, so the bank stops being withdraw-only.
-- Not natively compiled, for the same cross-container reason as usp_TribeBank_Withdraw: it must also touch the
-- disk-based game.Characters and game.TribeBankLog tables. Interop Transact-SQL is allowed to EXEC a natively
-- compiled procedure (game.usp_TribeBank_Deposit) from within its own explicit transaction; that EXEC enlists
-- in the ambient transaction instead of committing independently, so the debit/credit/log-write stay atomic.
CREATE PROCEDURE game.usp_TribeBank_DepositFromCharacter @TribeId TINYINT,
                                                         @SlotIndex TINYINT,
                                                         @CharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE
        @Debited TABLE
                 (
                     Amount INT
                 );

    BEGIN
        TRANSACTION;

    UPDATE game.Characters
    SET Money        = 0,
        UpdatedAtUtc = SYSUTCDATETIME()
    OUTPUT DELETED.Money
        INTO @Debited (Amount)
    WHERE CharacterId = @CharacterId
      AND Money >= 1;

    IF
        @@ROWCOUNT = 0
        THROW 50212, N'Character has no money to deposit into the tribe bank.', 1;

    DECLARE
        @Amount INT = (SELECT Amount FROM @Debited);

    EXEC game.usp_TribeBank_Deposit @TribeId = @TribeId, @SlotIndex = @SlotIndex, @Amount = @Amount;

    DECLARE
        @NewSlotAmount INT;
    SELECT @NewSlotAmount = Amount
    FROM game.TribeBank WITH (SNAPSHOT)
    WHERE TribeId = @TribeId
      AND SlotIndex = @SlotIndex;

    INSERT INTO game.TribeBankLog (TribeId, SlotIndex, CharacterId, Delta, BalanceAfter)
    VALUES (@TribeId, @SlotIndex, @CharacterId, @Amount, @NewSlotAmount);

    COMMIT TRANSACTION;

    SELECT Money
    FROM game.Characters
    WHERE CharacterId = @CharacterId;
END;
