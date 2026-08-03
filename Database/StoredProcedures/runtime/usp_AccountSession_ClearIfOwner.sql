CREATE PROCEDURE runtime.usp_AccountSession_ClearIfOwner @AccountId INT,
                                                         @ServerKind TINYINT,
                                                         @ShardId TINYINT NULL,
                                                         @SessionToken UNIQUEIDENTIFIER
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM runtime.AccountSessions
    WHERE AccountId = @AccountId
      AND ServerKind = @ServerKind
      AND (@ShardId IS NULL OR ShardId = @ShardId)
      AND SessionToken = @SessionToken;
END;
