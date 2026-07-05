-- TVP for usp_Character_CreateWithStarterKit: one row per learned skill slot (row absence = empty slot,
-- same convention as game.CharacterSkills itself).
CREATE TYPE game.tvp_CharacterSkillSlot AS TABLE
    (
    SlotIndex TINYINT NOT NULL,
    SkillId INT NOT NULL,
    Grade INT NOT NULL
    );
