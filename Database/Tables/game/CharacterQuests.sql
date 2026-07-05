-- Legacy: wAvatar.aQuestInfo[5] -> [0]=StepPermanent, [1]=ActiveQuestId, [2]=QSort (quest type),
-- [3]=TargetPhase, [4]=KillCounter. At most one row per character; no row = chain never touched
-- (usp_Character_GetForWorldEntry folds it in via LEFT JOIN + ISNULL(0)).
CREATE TABLE game.CharacterQuests
(
    CharacterId   INT NOT NULL,
    StepPermanent INT NOT NULL CONSTRAINT DF_CharacterQuests_StepPermanent DEFAULT 0,
    ActiveQuestId INT NOT NULL CONSTRAINT DF_CharacterQuests_ActiveQuestId DEFAULT 0,
    QSort         INT NOT NULL CONSTRAINT DF_CharacterQuests_QSort DEFAULT 0,
    TargetPhase   INT NOT NULL CONSTRAINT DF_CharacterQuests_TargetPhase DEFAULT 0,
    KillCounter   INT NOT NULL CONSTRAINT DF_CharacterQuests_KillCounter DEFAULT 0,
    CONSTRAINT PK_CharacterQuests PRIMARY KEY CLUSTERED (CharacterId),
    CONSTRAINT FK_CharacterQuests_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId)
);
