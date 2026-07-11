-- database/50_procedures/game/usp_TribeBank_ApplyTaxSweep.sql
-- The durable half of the 10-zone-uptime-minute tribe-bank income-tax sweep (behavior contract C17, side
-- effect A3 / legacy ts25playuser SAVE handler, Server/ts25playuser/S04_MyWork02.cpp:280-312). Replaces the
-- LoggingOnlyTribeBankTaxSweepGateway placeholder: for each of the four tribes carrying a positive incoming
-- amount, the WHOLE incoming amount is deposited into the FIRST of that tribe's 50 slots (0-49) that can
-- absorb it without breaching the 2,000,000,000 legacy money ceiling (MAX_NUMBER_SIZE, DEFINE.h:365). If no
-- slot can absorb the full amount (every slot would overflow), the first slot still below the ceiling is
-- force-set (clamped) UP to the ceiling -- a saturating fallback that keeps at most the ceiling and drops the
-- rest. If every one of the 50 slots is already at the ceiling, the incoming amount is dropped silently with
-- no error, exactly matching the legacy's own best-effort fire-and-forget sweep (a lost sweep destroys that
-- window's tax with no retry -- see TribeBankTaxAccumulator.TrySweep / TribeBankTaxSweepFlushHost).
--
-- This is why game.usp_TribeBank_Deposit (native, single-slot, no ceiling check, no scan-for-room) cannot
-- implement the contract as written -- see ITribeBankTaxSweepGateway's own remarks. This procedure is the
-- scan-for-room / cap-fallback merge that proc always lacked.
--
-- Disk-based (interop), NOT natively compiled: it needs a recursive CTE to enumerate the 50 slots (including
-- slots with no materialized row, which are implicitly 0), which native compilation does not support. It
-- touches ONLY the memory-optimized game.TribeBank (no disk-based table, no character money -- a system tax
-- accrual, not a character action, so unlike usp_TribeBank_Withdraw there is nothing to log). game.TribeBank
-- reads use WITH (SNAPSHOT): READ_COMMITTED_SNAPSHOT is ON database-wide, so any explicit-transaction
-- statement that reads a memory-optimized table alongside a disk-based one (here the @Incoming/@Targets table
-- variables) needs a supported isolation hint (MSSQLSERVER_41368/41359) -- same pattern as
-- usp_TribeBank_Withdraw. The RowExists flag captured while choosing the target slot lets the write phase
-- split cleanly into an UPDATE (existing rows) and an INSERT (missing rows) with no further memory-optimized
-- read.
--
-- Concurrency: a rare write-write conflict with another zone's concurrent sweep (or a player deposit/withdraw)
-- surfaces as SNAPSHOT error 41302 and aborts this sweep -- which the flush host catches and logs as that
-- window's tax lost. That is the contract's own accepted best-effort failure mode, so there is no retry here.
--
-- No THROW: money that cannot be placed is dropped, never an error, so there is no admin.ErrorCatalog entry.
CREATE PROCEDURE game.usp_TribeBank_ApplyTaxSweep @Tribe0Amount BIGINT,
                                                  @Tribe1Amount BIGINT,
                                                  @Tribe2Amount BIGINT,
                                                  @Tribe3Amount BIGINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DECLARE
        @Ceiling INT = 2000000000;
    -- MAX_NUMBER_SIZE (Server/Header/Protocol/DEFINE.h:365).

    -- Positive incoming amounts only, clamped to the ceiling defensively (the zone-local accumulator already
    -- guarantees <= @Ceiling, but a value above it must never be stored). A zero/negative tribe contributes
    -- nothing and is skipped entirely, matching the accumulator's own "no partial credit" contract.
    DECLARE
        @Incoming TABLE
                  (
                      TribeId TINYINT PRIMARY KEY,
                      Amount  INT NOT NULL
                  );

    INSERT INTO @Incoming (TribeId, Amount)
    SELECT v.TribeId,
           CAST(CASE WHEN v.Amount > @Ceiling THEN @Ceiling ELSE v.Amount END AS INT)
    FROM (VALUES (CAST(0 AS TINYINT), @Tribe0Amount),
                 (CAST(1 AS TINYINT), @Tribe1Amount),
                 (CAST(2 AS TINYINT), @Tribe2Amount),
                 (CAST(3 AS TINYINT), @Tribe3Amount)) AS v(TribeId, Amount)
    WHERE v.Amount > 0;

    IF NOT EXISTS (SELECT 1 FROM @Incoming)
        RETURN;

    -- One chosen (TribeId, SlotIndex, NewAmount, RowExists) target per incoming tribe (a tribe whose money is
    -- dropped entirely produces no target). RowExists distinguishes an existing slot row (UPDATE) from an
    -- implicit-zero slot with no row yet (INSERT), so the write phase never has to re-read game.TribeBank.
    DECLARE
        @Targets TABLE
                 (
                     TribeId   TINYINT PRIMARY KEY,
                     SlotIndex TINYINT NOT NULL,
                     NewAmount INT     NOT NULL,
                     RowExists BIT     NOT NULL
                 );

    BEGIN
        TRANSACTION;

    WITH Nums AS (SELECT CAST(0 AS TINYINT) AS SlotIndex
                  UNION ALL
                  SELECT CAST(SlotIndex + 1 AS TINYINT)
                  FROM Nums
                  WHERE SlotIndex < 49),
         Grid AS (SELECT i.TribeId,
                         i.Amount                                                   AS Incoming,
                         n.SlotIndex,
                         COALESCE(tb.Amount, 0)                                     AS SlotAmount,
                         CAST(CASE WHEN tb.Amount IS NULL THEN 0 ELSE 1 END AS BIT) AS RowExists
                  FROM @Incoming i
                           CROSS JOIN Nums n
                           LEFT JOIN game.TribeBank tb WITH (SNAPSHOT)
                                     ON tb.TribeId = i.TribeId AND tb.SlotIndex = n.SlotIndex),
         -- First (lowest-index) slot that can absorb the whole incoming amount without breaching the ceiling.
         -- A missing slot is 0, and 0 + incoming <= ceiling always holds, so the first empty slot always
         -- qualifies -- the absorb path fails to find a target only when all 50 slots already exist and are
         -- too full, which is exactly when the clamp fallback below is meant to run.
         Absorbers AS (SELECT TribeId,
                              SlotIndex,
                              CAST(CAST(SlotAmount AS BIGINT) + Incoming AS INT)          AS NewAmount,
                              RowExists,
                              ROW_NUMBER() OVER (PARTITION BY TribeId ORDER BY SlotIndex) AS rn
                       FROM Grid
                       WHERE CAST(SlotAmount AS BIGINT) + Incoming <= @Ceiling),
         -- Clamp fallback: the first slot still below the ceiling, force-set UP to the ceiling. Only used for
         -- a tribe with no absorber at all (all 50 slots too full to take the whole amount) -- in which case
         -- every slot has a materialized row, so RowExists is always 1 here.
         Clampers AS (SELECT TribeId,
                             SlotIndex,
                             @Ceiling                                                    AS NewAmount,
                             RowExists,
                             ROW_NUMBER() OVER (PARTITION BY TribeId ORDER BY SlotIndex) AS rn
                      FROM Grid
                      WHERE SlotAmount < @Ceiling)
    INSERT
    INTO @Targets (TribeId, SlotIndex, NewAmount, RowExists)
    SELECT a.TribeId, a.SlotIndex, a.NewAmount, a.RowExists
    FROM Absorbers a
    WHERE a.rn = 1
    UNION ALL
    SELECT c.TribeId, c.SlotIndex, c.NewAmount, c.RowExists
    FROM Clampers c
    WHERE c.rn = 1
      AND NOT EXISTS (SELECT 1 FROM Absorbers a2 WHERE a2.TribeId = c.TribeId);

    -- Existing slots: overwrite to the computed post-merge amount.
    UPDATE tb
    SET Amount = t.NewAmount
    FROM game.TribeBank tb WITH (SNAPSHOT)
             JOIN @Targets t ON t.TribeId = tb.TribeId AND t.SlotIndex = tb.SlotIndex
    WHERE t.RowExists = 1;

    -- Slots with no materialized row yet: insert the computed amount (memory-optimized insert needs no
    -- isolation hint on the target; the source @Targets is a disk-based table variable).
    INSERT INTO game.TribeBank (TribeId, SlotIndex, Amount)
    SELECT t.TribeId, t.SlotIndex, t.NewAmount
    FROM @Targets t
    WHERE t.RowExists = 0;

    COMMIT TRANSACTION;
END;
