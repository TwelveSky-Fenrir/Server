-- Normalizes the legacy learned-skill array wAvatar.aSkill[40][2] (wire AvatarInfo.Skill[80] = 40 slots
-- x 2 ints: [slot][0] = skill number, [slot][1] = invested grade/upgrade value) into one row per
-- actually-learned slot -- a row simply not existing IS "slot empty", same normalization rule as
-- game.CharacterItems/GuildMembers.
-- SlotIndex is kept as an explicit column (not derived from insertion order): the wire re-emits skills
-- in slot order and the client addresses skills by slot in CZ packets, so the slot IS protocol state.
-- SkillId is a real FK to world.Skills (the legacy "0 = empty slot" sentinel translates to row-absence);
-- Grade is stored verbatim as the legacy int -- its semantics (grade points vs. levels) belong to
-- StatCalculator/skill logic (Phase C), not to storage.
CREATE TABLE game.CharacterSkills
(
    CharacterId INT     NOT NULL,
    SlotIndex   TINYINT NOT NULL,
    SkillId     INT     NOT NULL,
    Grade       INT     NOT NULL CONSTRAINT DF_CharacterSkills_Grade DEFAULT 0,
    CONSTRAINT PK_CharacterSkills PRIMARY KEY CLUSTERED (CharacterId, SlotIndex),
    CONSTRAINT CK_CharacterSkills_SlotIndex CHECK (SlotIndex <= 39), -- aSkill[40]
    CONSTRAINT FK_CharacterSkills_Character FOREIGN KEY (CharacterId) REFERENCES game.Characters (CharacterId),
    CONSTRAINT FK_CharacterSkills_Skill FOREIGN KEY (SkillId) REFERENCES world.Skills (SkillId)
);
