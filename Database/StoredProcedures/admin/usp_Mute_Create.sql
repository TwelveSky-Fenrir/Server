CREATE PROCEDURE admin.usp_Mute_Create @AccountId INT NULL,
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
        THROW 50306, N'A mute must target at least one of @AccountId or @CharacterId.', 1;

    INSERT INTO admin.Mutes (AccountId, CharacterId, Reason, ExpiresAtUtc, ActorAccountId, ActorCharacterId)
    OUTPUT INSERTED.MuteId
    VALUES (@AccountId, @CharacterId, @Reason, @ExpiresAtUtc, @ActorAccountId, @ActorCharacterId);
END;
