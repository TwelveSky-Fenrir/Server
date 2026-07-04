-- Legacy mDropMoneyInfo[3], 1:1 extension (all-zero row = monster never drops money). DropRate is field[0], a rate numerator/threshold, not a plain drop-chance percentage; the roll formula that consumes it is not reverse-engineered.
CREATE TABLE world.MonsterDropMoney
(
    MonsterId INT NOT NULL,
    DropRate  INT NOT NULL,
    MinAmount INT NOT NULL,
    MaxAmount INT NOT NULL,
    CONSTRAINT PK_MonsterDropMoney PRIMARY KEY CLUSTERED (MonsterId),
    CONSTRAINT FK_MonsterDropMoney_Monster FOREIGN KEY (MonsterId) REFERENCES world.Monsters (MonsterId),
    CONSTRAINT CK_MonsterDropMoney_AmountRange CHECK (MinAmount <= MaxAmount)
);
