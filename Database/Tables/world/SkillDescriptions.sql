CREATE TABLE world.SkillDescriptions
(
    SkillId   INT          NOT NULL,
    LineIndex TINYINT      NOT NULL,
    Text      NVARCHAR(51) NOT NULL,
    CONSTRAINT PK_SkillDescriptions PRIMARY KEY CLUSTERED (SkillId, LineIndex),
    CONSTRAINT FK_SkillDescriptions_Skills FOREIGN KEY (SkillId) REFERENCES world.Skills (SkillId),
    CONSTRAINT CK_SkillDescriptions_LineIndex CHECK (LineIndex BETWEEN 0 AND 9)
);
