-- Legacy QUEST_INFO; QuestId 1..688 (312 of the raw 1000 slots are unused padding, identified by empty qSubject).
-- qReward[3][2] is a (RewardType, Value) tagged union, not an (itemId, quantity) pair -- see world.QuestRewards.
-- Solution1/2 are polymorphic on Sort (the quest-type discriminator) and therefore not FK'd:
--   Sort 1/5=Kill: Solution1=MonsterId, Solution2=kill count | Sort 2/3/4=Item quests: Solution1=ItemId
--   Sort 6=Exchange: Solution1/2=ItemId before/after | Sort 7=Meet NPC: unused | Sort 8=Event: Solution1=event/region id
-- NextIndex is NULL (never a literal 0, or 689 which is itself an excluded padding slot) to mean "end of chain".
CREATE TABLE world.Quests
(
    QuestId          INT          NOT NULL,
    Subject          NVARCHAR(51) NOT NULL,
    Category         TINYINT      NOT NULL,
    Step             SMALLINT     NOT NULL,
    Level            SMALLINT     NOT NULL,
    Type             TINYINT      NOT NULL,
    Sort             TINYINT      NOT NULL, -- quest-type discriminator, see header comment mapping
    SummonZoneNumber SMALLINT     NULL,     -- qSummonInfo[0]; NULL unless Sort=5 (boss summon)
    SummonPosX       INT          NULL,     -- qSummonInfo[1]
    SummonPosY       INT          NULL,     -- qSummonInfo[2]
    SummonPosZ       INT          NULL,     -- qSummonInfo[3]
    StartNPCNumber   INT          NOT NULL,
    KeyNpcNumber1    INT          NULL,     -- qKeyNPCNumber[0]; the only slot ever populated in this build
    KeyNpcNumber2    INT          NULL,
    KeyNpcNumber3    INT          NULL,
    KeyNpcNumber4    INT          NULL,
    KeyNpcNumber5    INT          NULL,
    EndNPCNumber     INT          NOT NULL,
    Solution1        INT          NULL,     -- qSolution[0]; polymorphic on Sort, see header comment -- no FK
    Solution2        INT          NULL,     -- qSolution[1]; polymorphic on Sort, see header comment -- no FK
    Solution3        INT          NULL,
    Solution4        INT          NULL,
    NextIndex        INT          NULL,
    CONSTRAINT PK_Quests PRIMARY KEY CLUSTERED (QuestId),
    CONSTRAINT FK_Quests_Level FOREIGN KEY (Level) REFERENCES world.Levels (Level),
    CONSTRAINT FK_Quests_SummonZone FOREIGN KEY (SummonZoneNumber) REFERENCES world.Zones (ZoneNumber),
    CONSTRAINT FK_Quests_StartNpc FOREIGN KEY (StartNPCNumber) REFERENCES world.Npcs (NpcId),
    CONSTRAINT FK_Quests_EndNpc FOREIGN KEY (EndNPCNumber) REFERENCES world.Npcs (NpcId),
    CONSTRAINT FK_Quests_KeyNpc1 FOREIGN KEY (KeyNpcNumber1) REFERENCES world.Npcs (NpcId),
    CONSTRAINT FK_Quests_KeyNpc2 FOREIGN KEY (KeyNpcNumber2) REFERENCES world.Npcs (NpcId),
    CONSTRAINT FK_Quests_KeyNpc3 FOREIGN KEY (KeyNpcNumber3) REFERENCES world.Npcs (NpcId),
    CONSTRAINT FK_Quests_KeyNpc4 FOREIGN KEY (KeyNpcNumber4) REFERENCES world.Npcs (NpcId),
    CONSTRAINT FK_Quests_KeyNpc5 FOREIGN KEY (KeyNpcNumber5) REFERENCES world.Npcs (NpcId),
    CONSTRAINT FK_Quests_NextQuest FOREIGN KEY (NextIndex) REFERENCES world.Quests (QuestId),
    CONSTRAINT CK_Quests_Category CHECK (Category BETWEEN 1 AND 4),
    CONSTRAINT CK_Quests_Type CHECK (Type BETWEEN 1 AND 2),
    CONSTRAINT CK_Quests_Sort CHECK (Sort BETWEEN 1 AND 8),
    -- Legacy never lets an out-of-range Step reach the shared-memory quest table at all
    -- (Quest_CheckValidElement, Server/Header/S15_MyShare.cpp:2003-2007) -- rejects the whole 1000-slot
    -- Load_Quest call on the first offending record, fatal to ts25sharemem's boot sequence. Step ranges
    -- 1-207 across all 688 seeded quests (Migrations/Seed/world/050_quests.sql), comfortably inside this bound.
    CONSTRAINT CK_Quests_Step CHECK (Step BETWEEN 1 AND 1000)
);
