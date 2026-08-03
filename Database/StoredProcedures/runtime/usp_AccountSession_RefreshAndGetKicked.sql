CREATE PROCEDURE runtime.usp_AccountSession_RefreshAndGetKicked @ServerKind TINYINT,
                                                                @ShardId TINYINT NULL,
                                                                @AccountIds runtime.tvp_AccountIdList READONLY
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
             INNER JOIN @AccountIds AS a
                        ON a.AccountId = s.AccountId
    WHERE s.ServerKind = @ServerKind
      AND (@ShardId IS NULL
        OR s.ShardId = @ShardId);

    SELECT s.AccountId, s.SessionToken
    FROM runtime.AccountSessions AS s WITH (SNAPSHOT)
             INNER JOIN @AccountIds AS a
                        ON a.AccountId = s.AccountId
    WHERE s.KickRequested = 1
      AND s.ServerKind = @ServerKind
      AND (@ShardId IS NULL
        OR s.ShardId = @ShardId);
END;
