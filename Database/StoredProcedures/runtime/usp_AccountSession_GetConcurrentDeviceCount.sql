CREATE PROCEDURE runtime.usp_AccountSession_GetConcurrentDeviceCount @ExcludingAccountId INT,
                                                                     @AdapterIdentifier VARCHAR(128),
                                                                     @LocalIp VARCHAR(45),
                                                                     @RemoteIp VARCHAR(45)
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT COUNT(DISTINCT AccountId)
    FROM runtime.AccountSessions WITH (SNAPSHOT)
    WHERE AdapterIdentifier = @AdapterIdentifier
      AND LocalIp = @LocalIp
      AND RemoteIp = @RemoteIp
      AND AccountId <> @ExcludingAccountId;
END;
