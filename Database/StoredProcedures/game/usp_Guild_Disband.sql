-- game.GuildNotices access requires WITH (SNAPSHOT): the database does not set
-- MEMORY_OPTIMIZED_ELEVATE_TO_SNAPSHOT, so the hint is mandatory here, not defensive.
--
-- @CharacterId identifies the disbanding character for audit attribution. Also writes one game.EventLog row
-- (Category=GuildMoney=11, EventCode=3, DeltaMoney=0) in the same transaction -- see
-- usp_Guild_CreateAndDebitMoney's own header for the full GL_617_GUILD_MONEY audit-logging rationale. Legacy's
-- own GL_617_GUILD_MONEY call for disband logs a real event with MONEY=0, not a silent skip -- disband is
-- exactly as audit-worthy as create/upgrade even though no money moves.
CREATE PROCEDURE game.usp_Guild_Disband @GuildId INT, @CharacterId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Grade INT;
    DECLARE @ActorAccountId INT;
    DECLARE @AvatarName NVARCHAR(13);
    -- See usp_Guild_CreateAndDebitMoney's own comment above: EXEC cannot take CONCAT(...) inline.
    DECLARE @Payload NVARCHAR(MAX);

    BEGIN TRANSACTION;

    -- Read the guild's grade and the acting character's identity BEFORE the deletes below remove the guild
    -- row -- there is nothing left to join against afterward. Neither SELECT has a side effect, so doing
    -- them before the not-found check below is safe: if @GuildId doesn't exist, @Grade stays NULL and the
    -- deletes' final @@ROWCOUNT = 0 still throws 50235 before the EXEC is ever reached.
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
         @EventCode = 3, -- disband (legacy GL_617_GUILD_MONEY tAction=3)
         @Category = 11, -- game.EventLogCategory.GuildMoney
         @ActorAccountId = @ActorAccountId,
         @ActorCharacterId = @CharacterId,
         @DeltaMoney = 0,
         @Outcome = 1,
         @Payload = @Payload;

    COMMIT TRANSACTION;
END;
