CREATE PROCEDURE game.usp_Guild_Disband @GuildId INT, @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Grade INT;
    DECLARE @ActorAccountId INT;
    DECLARE @AvatarName NVARCHAR(13);
    DECLARE @Payload NVARCHAR(MAX);

    BEGIN TRANSACTION;

    SELECT @Grade = Grade
    FROM game.Guilds
    WHERE GuildId = @GuildId;

    SELECT @ActorAccountId = AccountId, @AvatarName = Name
    FROM game.Characters
    WHERE CharacterId = @CharacterId;

    DELETE
    FROM game.GuildNotices WITH (SNAPSHOT)
    WHERE GuildId = @GuildId;

    DELETE
    FROM game.GuildMembers
    WHERE GuildId = @GuildId;

    DELETE
    FROM game.Guilds
    WHERE GuildId = @GuildId;

    IF @@ROWCOUNT = 0
        THROW 50235, N'Guild not found.', 1;

    SET @Payload = CONCAT(N'GuildId=', @GuildId, N';AvatarName=', @AvatarName, N';Grade=', @Grade);

    EXEC game.usp_EventLog_Insert
         @EventCode = 3, 
         @Category = 11, 
         @ActorAccountId = @ActorAccountId,
         @ActorCharacterId = @CharacterId,
         @DeltaMoney = 0,
         @Outcome = 1,
         @Payload = @Payload;

    COMMIT TRANSACTION;
END;
