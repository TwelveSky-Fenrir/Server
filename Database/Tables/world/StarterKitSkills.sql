CREATE TABLE world.StarterKitSkills
(
    PreviousTribe TINYINT NOT NULL,
    SlotIndex     TINYINT NOT NULL,
    SkillId       INT     NOT NULL,
    Grade         INT     NOT NULL,
    CONSTRAINT PK_StarterKitSkills PRIMARY KEY CLUSTERED (PreviousTribe, SlotIndex),
    CONSTRAINT CK_StarterKitSkills_PreviousTribe CHECK (PreviousTribe BETWEEN 0 AND 2),
    CONSTRAINT CK_StarterKitSkills_SlotIndex CHECK (SlotIndex <= 39),
    CONSTRAINT FK_StarterKitSkills_Skill FOREIGN KEY (SkillId) REFERENCES world.Skills (SkillId)
);
