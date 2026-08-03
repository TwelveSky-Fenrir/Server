CREATE PROCEDURE admin.usp_Ban_Create @AccountId INT NULL,
                                      @CharacterId INT NULL,
                                      @Reason TINYINT,
                                      @ExpiresAtUtc DATETIME2(3) = NULL,
                                      @ActorAccountId INT = NULL,
                                      @ActorCharacterId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AccountId IS NULL AND @CharacterId IS NULL
        THROW 50301, N'A ban must target at least one of @AccountId or @CharacterId.', 1;

    INSERT INTO admin.Bans (AccountId, CharacterId, Reason, ExpiresAtUtc, ActorAccountId, ActorCharacterId)
    OUTPUT INSERTED.BanId
    VALUES (@AccountId, @CharacterId, @Reason, @ExpiresAtUtc, @ActorAccountId, @ActorCharacterId);
END;
