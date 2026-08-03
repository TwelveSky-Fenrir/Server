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
         Absorbers AS (SELECT TribeId,
                              SlotIndex,
                              CAST(CAST(SlotAmount AS BIGINT) + Incoming AS INT)          AS NewAmount,
                              RowExists,
                              ROW_NUMBER() OVER (PARTITION BY TribeId ORDER BY SlotIndex) AS rn
                       FROM Grid
                       WHERE CAST(SlotAmount AS BIGINT) + Incoming <= @Ceiling),
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

    UPDATE tb
    SET Amount = t.NewAmount
    FROM game.TribeBank tb WITH (SNAPSHOT)
             JOIN @Targets t ON t.TribeId = tb.TribeId AND t.SlotIndex = tb.SlotIndex
    WHERE t.RowExists = 1;

    INSERT INTO game.TribeBank (TribeId, SlotIndex, Amount)
    SELECT t.TribeId, t.SlotIndex, t.NewAmount
    FROM @Targets t
    WHERE t.RowExists = 0;

    COMMIT TRANSACTION;
END;
