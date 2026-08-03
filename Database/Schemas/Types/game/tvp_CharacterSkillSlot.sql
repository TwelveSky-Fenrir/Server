CREATE TYPE game.tvp_CharacterSkillSlot AS TABLE
(
    SlotIndex TINYINT NOT NULL,
    SkillId   INT     NOT NULL,
    Grade     INT     NOT NULL
);
