CREATE PROCEDURE runtime.usp_AccountSession_MarkTearingDown @AccountId INT,
                                                            @ServerKind TINYINT,
                                                            @ShardId TINYINT NULL,
                                                            @SessionToken UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    UPDATE runtime.AccountSessions
    SET SessionState = 1
    WHERE AccountId = @AccountId
      AND ServerKind = @ServerKind
      AND SessionToken = @SessionToken
      AND SessionState = 0
      AND ((@ServerKind = 0 AND @ShardId IS NULL AND ShardId IS NULL)
        OR (@ServerKind = 1 AND ShardId = @ShardId));
END;
