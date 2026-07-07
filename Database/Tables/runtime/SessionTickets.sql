-- Keyed on AccountId, not a random TicketId+Secret: the unmodified legacy client cannot present a
-- GUID/HMAC proof, only its own account identity -- a valid, unexpired ticket existing for that
-- AccountId is itself the proof, since only Login could have written it.
-- SCHEMA_ONLY: a ticket surviving a crash is worthless anyway (15s TTL).
CREATE TABLE runtime.SessionTickets
(
    AccountId    INT          NOT NULL,
    CharacterId  INT          NOT NULL,
    ShardId      TINYINT      NOT NULL,
    ExpiresAtUtc DATETIME2(3) NOT NULL,
    CONSTRAINT PK_SessionTickets PRIMARY KEY NONCLUSTERED HASH (AccountId)
        WITH (BUCKET_COUNT = 1024)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
