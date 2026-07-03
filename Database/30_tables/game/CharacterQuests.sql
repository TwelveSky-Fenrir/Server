-- Persists wAvatar.aQuestInfo[5], the ENTIRE legacy quest state (report 04 §5: one single linear quest
-- chain per tribe, no quest log): at most ONE row per character -- PK is CharacterId alone, the table
-- is plural only to match the domain's table naming. Column-to-legacy mapping (aQuestInfo index):
--   StepPermanent  <- [0] step courant -- the permanent chain progression; survives completion/abandon
--                     (tSort 2/5 reset [1..4] but never [0], report 04 §5).
--   ActiveQuestId  <- [1] flag "quete active" (legacy stores 0/1; kept INT verbatim, the name follows
--                     the phase-A3 brief -- 0 = idle is the only load-bearing semantic).
--   QSort          <- [2] type de quete (1=kill, 2=drop, 3=livraison, 4=reception, 5=capitaine,
--                     6=echange 2 phases).
--   TargetPhase    <- [3] item cible / phase d'echange (qSort 6 uses it as the phase counter).
--   KillCounter    <- [4] compteur de kills / 2e item (incremented by the monster-death hook when
--                     state == 2 and the id matches).
-- No FK on StepPermanent/TargetPhase to world.Quests: they are a chain index and an item/phase value,
-- not QuestId references (mQUEST.Search resolves (tribe, step) -> quest at runtime from WorldDataCache).
-- A character with no row = a character that never touched the quest chain (all-zeros aQuestInfo);
-- usp_Character_GetForWorldEntry folds this row into RS1 via LEFT JOIN + ISNULL(0) for exactly that
-- reason -- the legacy state is inline avatar state, not a list.
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
