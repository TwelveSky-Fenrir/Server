-- Normalizes legacy `worldinfo`'s mPossibleAllianceInfo[MAX_TRIBE_NUM][2] (confirmed in Protocol/STRUCT.h's
-- WORLD_INFO struct; the dump's own mPossibleAllianceInfo0_0/0_1/1_0/1_1/2_0/2_1/3_0/3_1 columns are exactly
-- this 4x2=8-int matrix flattened) into one row per (offering tribe, candidate tribe) pair, instead of 8
-- fixed columns on the WorldState singleton.
-- CK_..._NotSelf (a tribe cannot ally with itself) also resolves a real source ambiguity: the dump's default
-- value for every slot is 0, which is indistinguishable from "no candidate set" EXCEPT for tribe 0's own two
-- slots, where a literal 0 would mean "tribe 0 offering an alliance to tribe 0". The CHECK constraint forces
-- that specific case to be treated as "no candidate" too, the same as every other tribe's resting 0, so the
-- ambiguity resolves itself by construction rather than needing a separate sentinel.
-- IsAccepted DOES NOT have a clean legacy source: `mAllianceState[2][2]` (also confirmed in WORLD_INFO) uses
-- a different, smaller 2x2 indexing scheme than mPossibleAllianceInfo's 4x2 candidate matrix, so there is no
-- byte-exact mapping from the legacy accept-state ints onto a per-candidate-pair flag. IsAccepted is
-- included per this table's brief-given shape and defaults to false; real values are left to the
-- application layer once the tribe-alliance feature is implemented, rather than guessing a wrong
-- correspondence -- moot for now since this table starts empty regardless.
-- PLAIN (not memory-optimized) table: needs enforced FOREIGN KEYs to game.Tribes (regular disk-based -- see
-- game.HeroRankings for the same In-Memory OLTP constraint), and alliance offers change rarely.
-- Starts empty (no existing players yet) -- schema only, no seed, per this domain's brief.
CREATE TABLE game.WorldStateAllianceOffers
(
    FromTribeId TINYINT NOT NULL,
    ToTribeId   TINYINT NOT NULL,
    IsAccepted  BIT     NOT NULL CONSTRAINT DF_WorldStateAllianceOffers_IsAccepted DEFAULT 0,
    CONSTRAINT PK_WorldStateAllianceOffers PRIMARY KEY CLUSTERED (FromTribeId, ToTribeId),
    CONSTRAINT CK_WorldStateAllianceOffers_NotSelf CHECK (FromTribeId <> ToTribeId),
    CONSTRAINT FK_WorldStateAllianceOffers_FromTribe FOREIGN KEY (FromTribeId) REFERENCES game.Tribes (TribeId),
    CONSTRAINT FK_WorldStateAllianceOffers_ToTribe FOREIGN KEY (ToTribeId) REFERENCES game.Tribes (TribeId)
);
