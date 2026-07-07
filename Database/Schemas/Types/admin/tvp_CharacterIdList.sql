-- Batched liveness/mute-refresh input, mirroring runtime.tvp_AccountIdList's own single-column shape. Plain
-- disk-based table type -- the procedure that consumes it is interpreted T-SQL, not natively compiled.
CREATE TYPE admin.tvp_CharacterIdList AS TABLE
(
    CharacterId INT NOT NULL
);
