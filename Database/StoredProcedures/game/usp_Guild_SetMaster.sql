CREATE PROCEDURE game.usp_Guild_SetMaster @GuildId INT,
                                          @NewMasterCharacterId INT
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
        NOT EXISTS (SELECT 1
                    FROM game.GuildMembers
                    WHERE GuildId = @GuildId
                      AND CharacterId = @NewMasterCharacterId)
        THROW 50233, N'Character is not a member of this guild.', 1;

    BEGIN
        TRANSACTION;

    UPDATE game.GuildMembers
    SET Role         = 0, 
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE GuildId = @GuildId
      AND Role = 2
      AND CharacterId <> @NewMasterCharacterId;

    UPDATE game.GuildMembers
    SET Role         = 2,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE GuildId = @GuildId
      AND CharacterId = @NewMasterCharacterId;

    UPDATE game.Guilds
    SET MasterCharacterId = @NewMasterCharacterId,
        UpdatedAtUtc      = SYSUTCDATETIME()
    WHERE GuildId = @GuildId;

    COMMIT TRANSACTION;
END;
