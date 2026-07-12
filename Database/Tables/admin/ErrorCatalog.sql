-- Catalog of THROW 50xxx errors a Fenrir procedure can raise. Ranges: 501xx auth, 502xx game,
-- 504xx runtime, 509xx cross-cutting/generic.
--
-- Collision rule (verified 2026-07-12 against all THROW sites under Database/StoredProcedures/**):
-- an ErrorNumber may be reused by more than one procedure ONLY when every thrower means the exact same
-- conceptual failure (e.g. 50235 "guild not found" across 7 guild procedures, 50350/50351 pet-bag
-- slot-occupied/empty across Deposit/Rearrange/Withdraw). Reusing a number for two genuinely different
-- failure kinds is never acceptable -- give the new kind its own next-free number instead. When a number
-- is reused, Description must name every current thrower (slash-separated) so the list can't silently
-- drift stale as procedures are added or renamed; refresh it in a new Migrations/Seed/admin/0NN_*.sql
-- (UPDATE, not INSERT, since the row already exists) rather than leaving an old proc's name behind.
CREATE TABLE admin.ErrorCatalog
(
    ErrorNumber INT           NOT NULL,
    SchemaName  VARCHAR(10)   NOT NULL,
    Description NVARCHAR(400) NOT NULL,
    CONSTRAINT PK_ErrorCatalog PRIMARY KEY CLUSTERED (ErrorNumber),
    CONSTRAINT CK_ErrorCatalog_ErrorNumber CHECK (ErrorNumber BETWEEN 50000 AND 59999),
    CONSTRAINT CK_ErrorCatalog_SchemaName CHECK (SchemaName IN ('auth', 'game', 'runtime', 'admin', 'world'))
);
