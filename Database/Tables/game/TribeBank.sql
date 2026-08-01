-- Legacy: mTribeBankInfo[tribe][slot] (MAX_TRIBE_BANK_SLOT_NUM=50), one row per slot; MEMORY_OPTIMIZED
-- since this is the hottest write path in the domain.
-- No FK on TribeId: In-Memory OLTP tables can't FK to disk-based game.Tribes; enforced by the application.
CREATE TABLE game.TribeBank
(
    TribeId   TINYINT NOT NULL,
    SlotIndex TINYINT NOT NULL,
    Amount    INT     NOT NULL
        CONSTRAINT DF_TribeBank_Amount DEFAULT 0,
    CONSTRAINT PK_TribeBank PRIMARY KEY NONCLUSTERED (TribeId, SlotIndex),
    CONSTRAINT CK_TribeBank_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 49),
    CONSTRAINT CK_TribeBank_Amount CHECK (Amount >= 0)
)
    WITH (MEMORY_OPTIMIZED = ON, DURABILITY = SCHEMA_AND_DATA);
