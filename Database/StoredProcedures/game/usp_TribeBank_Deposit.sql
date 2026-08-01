-- database/50_procedures/game/usp_TribeBank_Deposit.sql
-- Replaces ZONE_TRIBE_BANK_SAVE_FOR_PLAYUSER_SEND's mGAME.mTribeBankInfo[tribe][slot] += money handling.
-- Update-then-insert-if-missing (not DELETE-then-INSERT): this table accumulates a running balance
-- across calls, unlike most other upsert procs in this schema.
-- No direct C# repository caller -- its real caller is usp_TribeBank_DepositFromCharacter.sql (EXEC'd from
-- within that procedure's own explicit transaction; the natively compiled EXEC enlists in the ambient
-- transaction rather than committing independently, so the debit/credit/log-write stay atomic). Do not
-- delete this as "unused" from a C#-callsite grep alone -- always also check for an internal EXEC caller
-- among sibling stored procedures first (2026-07-12 Database/ cleanup pass note: this exact mistake was
-- made and reverted in the same session).
-- RECONFIRMED twice since (dead-file-sweep-tables-procs same day, then dead-file-sweep-game-world a third
-- time): both re-flagged this as a "superseded primitive" purely from a C# call-site grep, with no awareness
-- of the internal EXEC below. Declined the deletion both times for the same reason -- the EXEC reference at
-- usp_TribeBank_DepositFromCharacter.sql:48 was still there both times. Verify it's still there before
-- trusting a future re-raise of this finding.
--
-- Concurrency (procs-game-guilds-tribes-worldstate audit, severite moyenne, now closed): two characters
-- depositing into the exact same never-used (TribeId, SlotIndex) slot at the same instant can both see the
-- UPDATE below find zero rows and both attempt the INSERT -- a natively compiled procedure cannot take a
-- table hint (UPDLOCK/HOLDLOCK are rejected outright against MEMORY_OPTIMIZED tables, Microsoft Learn
-- "Accessing Memory-Optimized Tables Using Interpreted Transact-SQL") to close this race the way a
-- disk-based upsert would. The loser surfaces either an immediate duplicate-key error (2627/2601) or one of
-- the SNAPSHOT-isolation conflict codes (41302/41305/41325 -- per Microsoft Learn's "Transactions with
-- memory-optimized tables", a PRIMARY KEY/UNIQUE violation caused by a concurrent transaction can itself
-- surface as 41325, not only the immediate 2627), and the whole ambient
-- usp_TribeBank_DepositFromCharacter transaction rolls back under that procedure's own XACT_ABORT ON -- no
-- money or item is ever lost or duplicated, the loser just sees a raw SQL error. TribeRepository.DepositBankAsync
-- retries the whole usp_TribeBank_DepositFromCharacter call a small bounded number of times on any of those
-- five error numbers, mirroring AccountSessionRepository's ClaimOrSignalKick-family retry for the SNAPSHOT
-- codes plus usp_Character_ApplyTribeFourConversion's 2627/2601 catch for the same
-- never-used-composite-key-race shape -- safe because the character's debited Money rolls back along with
-- everything else, so a retried call finds it exactly as it was before the failed attempt. No change to this
-- procedure's own body was needed or possible; the fix lives entirely in the C# retry.
CREATE PROCEDURE game.usp_TribeBank_Deposit @TribeId TINYINT,
                                            @SlotIndex TINYINT,
                                            @Amount INT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    IF @Amount < 1
        THROW 50211, N'Tribe bank amount must be positive.', 1;

    UPDATE game.TribeBank
    SET Amount = Amount + @Amount
    WHERE TribeId = @TribeId
      AND SlotIndex = @SlotIndex;

    IF
        @@ROWCOUNT = 0
        BEGIN
            INSERT INTO game.TribeBank (TribeId, SlotIndex, Amount)
            VALUES (@TribeId, @SlotIndex, @Amount);
        END;
END;
