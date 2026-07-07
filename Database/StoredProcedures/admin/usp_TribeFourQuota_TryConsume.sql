-- Atomic check-and-increment: advances admin.TribeFourQuota.CurrentCount by one and reports success only if
-- doing so would not exceed MaxCount -- the single UPDATE's WHERE clause makes the check-then-act atomic
-- under SQL Server's own row-level locking, so two concurrent callers can never both be granted the last
-- remaining slot. Never throws on exhaustion -- a denied request is an expected, normal outcome (Granted=0),
-- not an error.
CREATE PROCEDURE admin.usp_TribeFourQuota_TryConsume
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE admin.TribeFourQuota
    SET CurrentCount = CurrentCount + 1
    WHERE Id = 1
      AND CurrentCount < MaxCount;

    SELECT Granted = CAST(CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS BIT);
END;
