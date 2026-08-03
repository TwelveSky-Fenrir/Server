CREATE TABLE world.TribeSkillEquivalences
(
    GroupIndex TINYINT NOT NULL,
    TribeId    TINYINT NOT NULL,
    SkillId    INT     NOT NULL,
    CONSTRAINT PK_TribeSkillEquivalences PRIMARY KEY CLUSTERED (GroupIndex, TribeId),
    CONSTRAINT CK_TribeSkillEquivalences_TribeId CHECK (TribeId BETWEEN 0 AND 2),
    CONSTRAINT UQ_TribeSkillEquivalences_Tribe_Skill UNIQUE (TribeId, SkillId),
    CONSTRAINT FK_TribeSkillEquivalences_Skill FOREIGN KEY (SkillId) REFERENCES world.Skills (SkillId)
);
