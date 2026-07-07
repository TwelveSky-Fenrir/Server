-- Grade is set to 1 explicitly, not the table's DEFAULT 0: legacy CreateGuild hard-codes gGrade=1, and
-- the grade-upgrade switch has no case for grade 0 (a guild left at 0 could never upgrade).
CREATE PROCEDURE game.usp_Guild_Create @Name NVARCHAR(12),
                                       @MasterCharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    IF
        EXISTS (SELECT 1 FROM game.Guilds WHERE Name = @Name)
        THROW 50230, N'Guild name is already taken.', 1;

    IF
        EXISTS (SELECT 1 FROM game.GuildMembers WHERE CharacterId = @MasterCharacterId)
        THROW 50231, N'Character already belongs to a guild.', 1;

    DECLARE
        @GuildId INT;

    BEGIN
        TRANSACTION;

    INSERT INTO game.Guilds (Name, MasterCharacterId, Grade)
    VALUES (@Name, @MasterCharacterId, 1);

    SET
        @GuildId = SCOPE_IDENTITY();

    INSERT INTO game.GuildMembers (GuildId, CharacterId, Role)
    VALUES (@GuildId, @MasterCharacterId, 2); -- 2 = master (game.GuildMembers role enum)

    COMMIT TRANSACTION;

    SELECT @GuildId AS GuildId;
END;
