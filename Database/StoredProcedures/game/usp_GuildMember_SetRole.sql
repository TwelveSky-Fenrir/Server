-- Role: 0 member, 1 sub-master; role 2 (master) must go through usp_Guild_SetMaster instead.
CREATE PROCEDURE game.usp_GuildMember_SetRole @GuildId INT,
                                              @CharacterId INT,
                                              @Role TINYINT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.GuildMembers
    SET Role = @Role
    WHERE GuildId = @GuildId
      AND CharacterId = @CharacterId;

    IF
        @@ROWCOUNT = 0
        THROW 50233, N'Character is not a member of this guild.', 1;
END;
