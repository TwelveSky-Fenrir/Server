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
