CREATE PROCEDURE game.usp_Guild_CreateAndDebitMoney @Name NVARCHAR(12),
                                                    @MasterCharacterId INT,
                                                    @DeltaMoney BIGINT,
                                                    @DeltaBigMoney INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF EXISTS (SELECT 1 FROM game.Guilds WHERE Name = @Name)
        THROW 50230, N'Guild name is already taken.', 1;

    IF EXISTS (SELECT 1 FROM game.GuildMembers WHERE CharacterId = @MasterCharacterId)
        THROW 50231, N'Character already belongs to a guild.', 1;

    DECLARE @GuildId INT;
    DECLARE @ActorAccountId INT;
    DECLARE @AvatarName NVARCHAR(13);
    DECLARE @Payload NVARCHAR(MAX);

    BEGIN TRANSACTION;

    UPDATE game.Characters
    SET Money        = Money + @DeltaMoney,
        BigMoney     = BigMoney + @DeltaBigMoney,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE CharacterId = @MasterCharacterId
      AND Money + @DeltaMoney BETWEEN 0 AND 2000000000
      AND BigMoney + @DeltaBigMoney >= 0;

    IF @@ROWCOUNT = 0
        BEGIN
            IF EXISTS (SELECT 1
                       FROM game.Characters
                       WHERE CharacterId = @MasterCharacterId
                         AND Money + @DeltaMoney > 2000000000)
                THROW 50261, N'Adjustment would exceed the legacy money cap (MAX_NUMBER_SIZE = 2,000,000,000).', 1;

            THROW 50277, N'Unknown character or insufficient money balance for the guild creation cost.', 1;
        END;

    SELECT @ActorAccountId = AccountId, @AvatarName = Name
    FROM game.Characters
    WHERE CharacterId = @MasterCharacterId;

    INSERT INTO game.Guilds (Name, MasterCharacterId, Grade)
    VALUES (@Name, @MasterCharacterId, 1);

    SET @GuildId = SCOPE_IDENTITY();

    INSERT INTO game.GuildMembers (GuildId, CharacterId, Role)
    VALUES (@GuildId, @MasterCharacterId, 2); 

    SET @Payload = CONCAT(N'GuildId=', @GuildId, N';AvatarName=', @AvatarName, N';Grade=1');

    EXEC game.usp_EventLog_Insert
         @EventCode = 1, 
         @Category = 11, 
         @ActorAccountId = @ActorAccountId,
         @ActorCharacterId = @MasterCharacterId,
         @DeltaMoney = @DeltaMoney,
         @DeltaBigMoney = @DeltaBigMoney,
         @Outcome = 1,
         @Payload = @Payload;

    COMMIT TRANSACTION;

    SELECT @GuildId AS GuildId;
END;
