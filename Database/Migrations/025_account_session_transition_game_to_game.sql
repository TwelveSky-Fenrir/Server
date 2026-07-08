-- Fix-forward, not an in-place edit: StoredProcedures/runtime/usp_AccountSession_TransitionToGame.sql is
-- already applied/journaled, so this corrective script CREATE OR ALTERs the same procedure name/signature
-- from a brand-new file instead of touching the original (see Database/_manifest.txt's own header and the
-- sql-server-2025-stored-procedure-conventions skill: "never edit an already-applied script").
--
-- Gap this closes (session-state-machine legacy-parity audit): the procedure's original predicate only
-- accepted a prior ServerKind = 0 (Login) row, modeling exclusively the Login->Game world-entry hand-off.
-- It never accounted for a Game(ShardA)->Game(ShardB) cross-shard hop: ZoneMoveService.HandleCrossShardAsync
-- mints a fresh runtime.SessionTickets row for the destination shard when a player walks into a map hosted
-- by a different shard, but the account's runtime.AccountSessions row is already ServerKind = 1 (Game) at
-- that moment -- the old `AND ServerKind = 0` predicate could never match, @@ROWCOUNT was always 0, and
-- ZoneHandshakeService.ConsumeTicketAsync's every cross-shard hop was answered with SessionSuperseded,
-- which ZoneHandshakeHandler turns into an unconditional Abort(DisconnectReason.Evicted) -- disconnecting
-- the player the instant they finished reconnecting to the new shard, every single time, with no retry path
-- (the single-use SessionTicket was already consumed).
--
-- Legacy itself never drew this distinction: the session broker's RegisterUserForZone_00 accepts a claim
-- when the PlayUser slot is in EITHER LP_MOVING_ZONE (3, first entry into any zone) or ZP_MOVING_ZONE (7,
-- zone-to-zone re-entry) -- "this single condition is what unifies 'first entry' and 'zone hop' into one
-- acceptance path" (Server/ts25playuser/S07_MyGame01.cpp:1032-1076, cited directly in this feature's
-- session-state-machine behavior contract, Preconditions section). Widening this procedure's own predicate
-- to accept a prior ServerKind of EITHER 0 (Login) or 1 (Game) is the direct Fenrir analog of that same
-- unification: the only real invariant is "the token still matches and the row isn't mid-teardown/kick", not
-- which server kind previously owned the row.
--
-- No C# contract change: IAccountSessionRepository.TransitionToGameAsync and its one call site
-- (ZoneHandshakeService.ConsumeTicketAsync) are untouched -- that call was already "claim this account's
-- session for Game at @ShardId given the SessionTicket's own token", which is exactly as true for a
-- zone-to-zone hop as it is for a fresh Login->Game entry. A stale/wrong-shard/expired ticket is still
-- refused earlier in ConsumeTicketAsync (tickets.ConsumeAsync's own ShardId check) before this procedure is
-- ever reached, and KickRequested/SessionState = 1 (TearingDown) still refuse the claim exactly as before --
-- widening ServerKind does not weaken either guard.
CREATE OR ALTER PROCEDURE runtime.usp_AccountSession_TransitionToGame @AccountId INT,
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
