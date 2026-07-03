-- Replaces TRIBE_BANK_INFO.DAT: a flat `int mTribeBankInfo[MAX_TRIBE_NUM][MAX_TRIBE_BANK_SLOT_NUM]`
-- (H07_MyGame.h; MAX_TRIBE_NUM=4, MAX_TRIBE_BANK_SLOT_NUM=50, Protocol/DEFINE.h) saved on every deposit/
-- withdraw (ZONE_TRIBE_BANK_SAVE_FOR_PLAYUSER_SEND, S04_MyWork02.cpp) -- the hottest write path in this
-- whole domain, unlike everything else here.
-- MEMORY_OPTIMIZED / SCHEMA_AND_DATA: this is real player-owned money, not disposable session state, so
-- (unlike runtime.SessionTickets' SCHEMA_ONLY) it must survive a restart.
-- One row per slot (not one row per tribe with 50 Amount columns): every slot is independently
-- deposit/withdraw-addressable by the wire protocol (tSlotIndex-equivalent), so unlike a truly sparse
-- fixed array this is closer to "50 real accounts per tribe" than "50 mostly-empty flags" -- but a slot
-- with Amount=0 is still legitimately absent-or-present depending on activity, and per-slot atomic
-- add/subtract procs are far simpler against narrow rows than a 50-column wide one.
-- PK is a NONCLUSTERED range index, not HASH, for the same reason as game.GuildNotices: the read path
-- (usp_TribeBank_GetByTribe) prefix-scans all 50 slots for one TribeId, which a HASH index (full-key
-- equality only) cannot serve without a table scan.
-- CK_TribeBank_Amount backstops usp_TribeBank_Withdraw's explicit insufficient-funds THROW (50210) --
-- defense in depth against any future write path that bypasses that proc.
-- NOTE -- no FK on TribeId to game.Tribes: same In-Memory OLTP limitation as game.GuildNotices (FOREIGN
-- KEY on a memory-optimized table is only supported when the referenced table is ALSO memory-optimized,
-- and game.Tribes is a regular disk-based table) -- see that file's header for the full reasoning.
CREATE TABLE game.TribeBank
(
    TribeId   TINYINT NOT NULL,
    SlotIndex TINYINT NOT NULL,
    Amount    INT     NOT NULL CONSTRAINT DF_TribeBank_Amount DEFAULT 0,
    CONSTRAINT PK_TribeBank PRIMARY KEY NONCLUSTERED (TribeId, SlotIndex),
    CONSTRAINT CK_TribeBank_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 49),
    CONSTRAINT CK_TribeBank_Amount CHECK (Amount >= 0)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
