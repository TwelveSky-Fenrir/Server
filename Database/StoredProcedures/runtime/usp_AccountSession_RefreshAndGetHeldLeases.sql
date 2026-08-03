CREATE PROCEDURE runtime.usp_AccountSession_RefreshAndGetHeldLeases @ServerKind TINYINT,
                                                                    @ShardId TINYINT NULL,
                                                                    @Leases runtime.tvp_AccountSessionLease READONLY
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE s
    SET s.LastRefreshedUtc = SYSUTCDATETIME()
    FROM runtime.AccountSessions AS s
             WITH (SNAPSHOT)
             INNER JOIN @Leases AS l
                        ON l.AccountId = s.AccountId
                            AND l.SessionToken = s.SessionToken
    WHERE s.ServerKind = @ServerKind
      AND (@ShardId IS NULL
        OR s.ShardId = @ShardId);

    SELECT s.AccountId
    FROM runtime.AccountSessions AS s WITH (SNAPSHOT)
             INNER JOIN @Leases AS l
                        ON l.AccountId = s.AccountId
                            AND l.SessionToken = s.SessionToken
    WHERE s.ServerKind = @ServerKind
      AND (@ShardId IS NULL
        OR s.ShardId = @ShardId);
END;
