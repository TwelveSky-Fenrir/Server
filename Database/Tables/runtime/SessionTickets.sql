-- Keyed on AccountId, not a random TicketId+Secret: the unmodified legacy client cannot present a
-- GUID/HMAC proof, only its own account identity -- a valid, unexpired ticket existing for that
-- AccountId is itself the proof, since only Login could have written it.
-- SCHEMA_ONLY: a ticket surviving a crash is worthless anyway (15s TTL).
CREATE TABLE runtime.SessionTickets
(
    AccountId    INT              NOT NULL,
    CharacterId  INT              NOT NULL,
    ShardId      TINYINT          NOT NULL,
    ExpiresAtUtc DATETIME2(3)     NOT NULL,
    SessionToken UNIQUEIDENTIFIER NOT NULL, -- minted by usp_AccountSession_ClaimOrSignalKick at Login-claim time; proves a Game-side world-entry claim is for the same login epoch as the one that minted this ticket
    AccountGrade SMALLINT         NOT NULL, -- carries auth.Accounts.AccountGrade across the Login->Game process boundary (GameServer never re-queries auth.Accounts)
    CONSTRAINT PK_SessionTickets PRIMARY KEY NONCLUSTERED HASH (AccountId)
        WITH (BUCKET_COUNT = 1024)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_ONLY);
