-- Legacy mDropMoneyInfo[3], 1:1 extension (all-zero row = monster never drops money). DropRate is field[0], a rate numerator/threshold, not a plain drop-chance percentage; the roll formula that consumes it is not reverse-engineered.
CREATE TABLE world.MonsterDropMoney
(
    MonsterId INT NOT NULL,
    DropRate  INT NOT NULL,
    MinAmount INT NOT NULL,
    MaxAmount INT NOT NULL,
    CONSTRAINT PK_MonsterDropMoney PRIMARY KEY CLUSTERED (MonsterId),
    CONSTRAINT FK_MonsterDropMoney_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT CK_MonsterDropMoney_AmountRange CHECK (MinAmount <= MaxAmount),
    -- Value-range bounds mirror legacy's load-time field validation (Monster_CheckValidElement,
    -- Server/Header/S15_MyShare.cpp:1768-1787).
    CONSTRAINT CK_MonsterDropMoney_DropRate CHECK (DropRate BETWEEN 0 AND 1000000),     -- :1768-1772
    CONSTRAINT CK_MonsterDropMoney_MinAmount CHECK (MinAmount BETWEEN 0 AND 100000000), -- :1773-1777
    CONSTRAINT CK_MonsterDropMoney_MaxAmount CHECK (MaxAmount BETWEEN 0 AND 100000000)  -- :1778-1787
);
