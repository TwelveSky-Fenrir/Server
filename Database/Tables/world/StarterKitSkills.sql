-- Legacy: same function's aSkill[40][2] block, one full table per PreviousTribe. Row absence = empty slot
-- (0 sentinel), same convention as game.CharacterSkills -- a real SkillId=0 row would violate the FK below
-- anyway (world.Skills has no id-0 row).
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
