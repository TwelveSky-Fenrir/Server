-- Any row present for this AdapterIdentifier/LocalIp/RemoteIp triple counts, regardless of ServerKind or
-- SessionState: a row only stops existing once the account has actually logged out. @ExcludingAccountId
-- excludes the account currently attempting to log in, so its own still-tracked row from a prior session
-- on this same device (e.g. a crash that hasn't been reaped yet) never counts against itself.
CREATE PROCEDURE runtime.usp_AccountSession_GetConcurrentDeviceCount @ExcludingAccountId INT,
                                                                     @AdapterIdentifier   VARCHAR(128),
                                                                     @LocalIp             VARCHAR(45),
                                                                     @RemoteIp            VARCHAR(45)
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
