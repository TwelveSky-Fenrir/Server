-- Idempotent: removing a non-member is a silent no-op (so a leave/kick race can't fail).
-- Removing the master is the caller's responsibility to prevent (transfer via usp_Guild_SetMaster first).
CREATE PROCEDURE game.usp_GuildMember_Remove @GuildId INT,
                                             @CharacterId INT
AS
BEGIN
    SET
        NOCOUNT ON;
    SET
        XACT_ABORT ON;

    DELETE
    FROM game.GuildMembers
    WHERE GuildId = @GuildId
      AND CharacterId = @CharacterId;
END;
