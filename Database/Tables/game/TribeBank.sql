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
