-- Legacy: wAvatar.aSkill[40][2] ([slot][0]=SkillId, [slot][1]=Grade); row absence = empty slot (0 sentinel).
CREATE TABLE game.CharacterSkills
(
    CharacterId INT     NOT NULL,
    SlotIndex   TINYINT NOT NULL,
    SkillId     INT     NOT NULL,
    Grade       INT     NOT NULL
        CONSTRAINT DF_CharacterSkills_Grade DEFAULT 0,
    CONSTRAINT PK_CharacterSkills PRIMARY KEY CLUSTERED (CharacterId, SlotIndex),
    CONSTRAINT CK_CharacterSkills_SlotIndex CHECK (SlotIndex <= 39), -- aSkill[40]
    CONSTRAINT FK_CharacterSkills_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    -- Cross-schema FK naming: see admin.Bans' own header comment for the FK_<ChildTable>_<TargetSchema>_<Role>
    -- convention -- the FK above stays bare (same-schema, game -> game).
    CONSTRAINT FK_CharacterSkills_World_Skill FOREIGN KEY (SkillId) REFERENCES world.Skills (SkillId)
    -- No secondary index on SkillId: verified repo-wide that every reader/writer drives its seek from
    -- CharacterId (the leading clustered-PK column) -- including the tribe-conversion skill remaps in
    -- usp_Character_ApplyTribeConversion/usp_Character_ApplyTribeScrollConversion, which join
    -- world.TribeSkillEquivalences ON src.SkillId = cs.SkillId but are still driven by cs.CharacterId = @X, so
    -- the join probes TribeSkillEquivalences's own index, never this table's SkillId column. Nothing ever
    -- seeks this table BY SkillId.
);
