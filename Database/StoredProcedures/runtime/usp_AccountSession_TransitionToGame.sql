-- Called at world-entry. Defers entirely to the same authority as usp_AccountSession_ClaimOrSignalKick --
-- this is a narrower read/write against the same row, not a second decision tree. Accepted only when the
-- account still holds the Login-side session matching @ExpectedSessionToken, which proves this claim is for
-- the same login epoch as the one that minted the SessionTicket carrying that token, not a hijack of a
-- newer login that raced ahead of the hand-off.
--
-- ServerKind IN (0, 1): accepts a prior session row of either Login (0) or Game (1) -- not just Login->Game
-- world entry, but also a Game(ShardA)->Game(ShardB) cross-shard hop, where ZoneMoveService.HandleCrossShardAsync
-- mints a fresh runtime.SessionTickets row for the destination shard while the account's runtime.AccountSessions
-- row is already ServerKind = 1 (Game). Legacy itself never drew this distinction: the session broker's
-- RegisterUserForZone_00 accepts a claim when the PlayUser slot is in EITHER LP_MOVING_ZONE (3, first entry
-- into any zone) or ZP_MOVING_ZONE (7, zone-to-zone re-entry) -- one condition unifying "first entry" and
-- "zone hop" into one acceptance path (Server/ts25playuser/S07_MyGame01.cpp:1032-1076, cited in this
-- feature's session-state-machine behavior contract, Preconditions section). Accepting either prior
-- ServerKind is the direct Fenrir analog: the only real invariant is "the token still matches and the row
-- isn't mid-teardown/kick", not which server kind previously owned the row. A stale/wrong-shard/expired
-- ticket is still refused earlier in ConsumeTicketAsync (tickets.ConsumeAsync's own ShardId check) before
-- this procedure is ever reached, and KickRequested/SessionState = 1 (TearingDown) still refuse the claim.
CREATE PROCEDURE runtime.usp_AccountSession_TransitionToGame @AccountId INT,
                                                             @ExpectedSessionToken UNIQUEIDENTIFIER,
                                                             @ShardId TINYINT
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    UPDATE runtime.AccountSessions
    SET ServerKind       = 1,
        ShardId          = @ShardId,
        LastRefreshedUtc = SYSUTCDATETIME()
    WHERE AccountId = @AccountId
      AND SessionToken = @ExpectedSessionToken
      AND ServerKind IN (0, 1)
      AND SessionState = 0
      AND KickRequested = 0;

    SELECT CAST(CASE WHEN @@ROWCOUNT = 1 THEN 1 ELSE 0 END AS BIT) AS Accepted;
END;
