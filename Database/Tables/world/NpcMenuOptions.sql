CREATE TABLE world.NpcMenuOptions
(
    NpcId     INT      NOT NULL,
    SlotIndex SMALLINT NOT NULL,
    OptionId  INT      NOT NULL,
    CONSTRAINT PK_NpcMenuOptions PRIMARY KEY CLUSTERED (NpcId, SlotIndex),
    CONSTRAINT CK_NpcMenuOptions_SlotIndex CHECK (SlotIndex BETWEEN 0 AND 99),
    CONSTRAINT FK_NpcMenuOptions_Npcs FOREIGN KEY (NpcId) REFERENCES world.Npcs (NpcId),
    CONSTRAINT CK_NpcMenuOptions_OptionId CHECK (OptionId IN (1, 2))
);
