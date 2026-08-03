CREATE TABLE game.CharacterSkills
(
    CharacterId INT     NOT NULL,
    SlotIndex   TINYINT NOT NULL,
    SkillId     INT     NOT NULL,
    Grade       INT     NOT NULL
        CONSTRAINT DF_CharacterSkills_Grade DEFAULT 0,
    CONSTRAINT PK_CharacterSkills PRIMARY KEY CLUSTERED (CharacterId, SlotIndex),
    CONSTRAINT CK_CharacterSkills_SlotIndex CHECK (SlotIndex <= 39), 
    CONSTRAINT FK_CharacterSkills_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_CharacterSkills_World_Skill FOREIGN KEY (SkillId) REFERENCES world.Skills (SkillId)
);
