CREATE PROCEDURE game.usp_Guild_UpgradeAndDebitMoney @GuildId INT,
                                                     @Grade INT,
                                                     @CharacterId INT,
                                                     @DeltaMoney BIGINT,
                                                     @DeltaBigMoney INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @ActorAccountId INT;
    DECLARE @AvatarName NVARCHAR(13);
    DECLARE @Payload NVARCHAR(MAX);

    BEGIN TRANSACTION;

    UPDATE game.Guilds
    SET Grade        = @Grade,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE GuildId = @GuildId
      AND Grade = @Grade - 1;

    IF @@ROWCOUNT = 0
        BEGIN
            IF NOT EXISTS (SELECT 1 FROM game.Guilds WHERE GuildId = @GuildId)
                THROW 50235, N'Guild not found.', 1;

            THROW 50365, N'Guild grade changed between the caller''s read and this upgrade write.', 1;
        END;

    UPDATE game.Characters
    SET Money        = Money + @DeltaMoney,
        BigMoney     = BigMoney + @DeltaBigMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @CharacterId
      AND Money + @DeltaMoney BETWEEN 0 AND 2000000000
      AND BigMoney + @DeltaBigMoney >= 0;

    IF @@ROWCOUNT = 0
        BEGIN
            IF EXISTS (SELECT 1
                       FROM game.Characters
                       WHERE CharacterId = @CharacterId
                         AND Money + @DeltaMoney > 2000000000)
                THROW 50261, N'Adjustment would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;

            THROW 50278, N'Unknown character or insufficient money balance for the guild upgrade cost.', 1;
        END;

    SELECT @ActorAccountId = AccountId, @AvatarName = Name
    FROM game.Characters
    WHERE CharacterId = @CharacterId;

    SET @Payload = CONCAT(N'GuildId=', @GuildId, N';AvatarName=', @AvatarName, N';Grade=', @Grade);

    EXEC game.usp_EventLog_Insert
         @EventCode = 2,
         @Category = 11,
         @ActorAccountId = @ActorAccountId,
         @ActorCharacterId = @CharacterId,
         @DeltaMoney = @DeltaMoney,
         @DeltaBigMoney = @DeltaBigMoney,
         @Outcome = 1,
         @Payload = @Payload;

    COMMIT TRANSACTION;
END;
