CREATE PROCEDURE game.usp_GuildMember_SetCallName @GuildId INT,
                                                  @CharacterId INT,
                                                  @CallName NVARCHAR(4)
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    UPDATE game.GuildMembers
    SET CallName     = @CallName,
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE GuildId = @GuildId
      AND CharacterId = @CharacterId;

    IF
        @@ROWCOUNT = 0
        THROW 50233, N'Character is not a member of this guild.', 1;
END;
