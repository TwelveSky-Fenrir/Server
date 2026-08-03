CREATE PROCEDURE game.usp_GuildMember_Add @GuildId INT,
                                          @CharacterId INT,
                                          @Role TINYINT = 0
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        NOT EXISTS (SELECT 1 FROM game.Guilds WHERE GuildId = @GuildId)
        THROW 50235, N'Guild not found.', 1;

    IF
        EXISTS (SELECT 1 FROM game.GuildMembers WHERE CharacterId = @CharacterId)
        THROW 50231, N'Character already belongs to a guild.', 1;

    DECLARE @Grade INT;
    DECLARE @MemberCount INT;

    BEGIN
        TRANSACTION;

    SELECT @Grade = Grade
    FROM game.Guilds
    WITH (UPDLOCK, HOLDLOCK)
    WHERE GuildId = @GuildId;

    SELECT @MemberCount = COUNT(*)
    FROM game.GuildMembers
    WITH (UPDLOCK, HOLDLOCK)
    WHERE GuildId = @GuildId;

    IF @MemberCount >= 50
        THROW 50232
            , N'Guild is full (50 members).'
            , 1;

    IF @MemberCount >= @Grade * 10
        THROW 50364
            , N'Guild is full for its current grade (Grade * 10 members).'
            , 1;

    INSERT INTO game.GuildMembers (GuildId, CharacterId, Role)
    VALUES (@GuildId, @CharacterId, @Role);

    COMMIT TRANSACTION;
END;
