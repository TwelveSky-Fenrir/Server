-- Transfers leadership: updates Guilds.MasterCharacterId plus the old and new master's member Role.
CREATE PROCEDURE game.usp_Guild_SetMaster @GuildId INT,
                                          @NewMasterCharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    -- Must precede the membership check, or a missing @GuildId always surfaces as 50233 instead of 50235.
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
    SET Role = 0 -- demote whoever currently holds master
    WHERE GuildId = @GuildId
      AND Role = 2
      AND CharacterId <> @NewMasterCharacterId;

    UPDATE game.GuildMembers
    SET Role = 2
    WHERE GuildId = @GuildId
      AND CharacterId = @NewMasterCharacterId;

    UPDATE game.Guilds
    SET MasterCharacterId = @NewMasterCharacterId
    WHERE GuildId = @GuildId;

    COMMIT TRANSACTION;
END;
