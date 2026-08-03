CREATE TABLE game.CharacterQuests
(
    CharacterId   INT NOT NULL,
    StepPermanent INT NOT NULL
        CONSTRAINT DF_CharacterQuests_StepPermanent DEFAULT 0,
    ActiveQuestId INT NOT NULL
        CONSTRAINT DF_CharacterQuests_ActiveQuestId DEFAULT 0,
    QSort         INT NOT NULL
        CONSTRAINT DF_CharacterQuests_QSort DEFAULT 0,
    TargetPhase   INT NOT NULL
        CONSTRAINT DF_CharacterQuests_TargetPhase DEFAULT 0,
    KillCounter   INT NOT NULL
        CONSTRAINT DF_CharacterQuests_KillCounter DEFAULT 0,
    CONSTRAINT PK_CharacterQuests PRIMARY KEY CLUSTERED (CharacterId),
    CONSTRAINT FK_CharacterQuests_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
