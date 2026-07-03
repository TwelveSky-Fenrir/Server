-- Keyed on AccountId, NOT a random TicketId+Secret (unlike the doc's native-protocol §8.2 example):
-- the unmodified legacy client cannot present a GUID/HMAC proof, only its own account identity
-- (the tID it relays to the zone, §3.3/§8 of the wire contract) -- see ADR-0005/ADR-0003 A-04.
-- A valid, unexpired, single-use ticket existing for that AccountId at all is the proof: only Login,
-- straight after authenticating that account, could have written it.
-- SCHEMA_ONLY (architecture reference §12.4): zero disk I/O/log; a ticket surviving a crash is
-- worthless anyway (15s TTL) so durability would be pure cost.
CREATE TABLE runtime.SessionTickets
(
    AccountId    INT     NOT NULL,
    CharacterId  INT     NOT NULL,
    ShardId      TINYINT NOT NULL,
    ExpiresAtUtc DATETIME2(3) NOT NULL,
    CONSTRAINT PK_SessionTickets PRIMARY KEY NONCLUSTERED HASH (AccountId)
        WITH (BUCKET_COUNT = 1024) -- generous vs. realistic concurrent-handover peaks for M1's scale
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
