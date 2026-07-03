-- Source: legacy QUEST_INFO (Header/Protocol/STRUCT.h:237-274), 005_00006.IMG via QuestReader.ReadAll (no
-- runtime patch exists for this file, ReadAll is byte-identical to ReadAllRaw). The 1000-slot array is 1-based
-- (Index itself is never 0, unlike Monsters/Npcs) but 312 of the 1000 slots are unused padding -- confirmed by
-- direct inspection to have an empty qSubject AND all-zero qStartNPCNumber/qEndNPCNumber/qNextIndex/reward/
-- speech content -- so "Subject is non-empty" is the real "is this slot used" signal here, leaving 688 real
-- quests (QuestId 1..688 in this build).
--
-- qReward[3][2] is NOT an (itemId, quantity) pair as the field name might suggest -- confirmed from the actual
-- game logic, not guessed: the switch in ts25zone/S04_MyWork02.cpp (~line 7404) and the bounds check in
-- Header/S15_MyShare.cpp (~line 2046, "qReward[i][0] < 1 || > 6") together prove it is a (RewardType, Value)
-- tagged union (2=Money, 3=KillOtherTribeCount, 4=Experience, 5=TeacherPoint, 6=Item-with-Value-as-ItemId;
-- 1=a deliberate "no reward" sentinel, case 1: break). See world.QuestRewards for the normalized child table.
--
-- qSummonInfo[4] is NOT four interchangeable scalars: SummonQuestBoss (ts25zone/S07_MyGame04.cpp) proves
-- qSummonInfo[0] is a zone number (compared against mSERVER_INFO.mServerNumber, bounds-checked 0..200 in
-- S15_MyShare.cpp) and qSummonInfo[1..3] are signed X/Y/Z world coordinates for where a quest boss is summoned
-- (cast to float at use-time, kept here as the legacy int values). Real data confirms this is populated for
-- exactly the 127 Sort=5 ("Kill the Captain") quests and every SummonZoneNumber used resolves to a real,
-- already-shipped world.Zones row -- so it is FK'd, unlike the polymorphic Solution columns below.
--
-- qKeyNPCNumber[5]/qSolution[4] are kept as small numbered columns per the task brief's explicit carve-out
-- ("small fixed arrays -- numbered columns are fine"), even though qKeyNPCNumber has 5 slots: real data shows
-- only KeyNpcNumber1 is EVER populated in this build (76 of 688 quests; slots 2-5 are always zero/NULL), and
-- only Solution1/Solution2 are ever populated (Solution3/Solution4 always NULL) -- kept at full legacy width
-- for fidelity, not because the data currently uses it.
-- Solution's meaning is genuinely polymorphic on Sort (the quest-type discriminator, see below) -- confirmed
-- from ts25zone/S04_MyWork02.cpp and S07_MyGame04(-Copy).cpp's per-Sort switches:
--   Sort=1 (Kill monsters):      Solution1=MonsterId, Solution2=required kill count
--   Sort=2 (Item acquisition):   Solution1=ItemId
--   Sort=3 (Item delivery):      Solution1=ItemId
--   Sort=4 (Receiving items):    Solution1=ItemId
--   Sort=5 (Kill the Captain):   Solution1=MonsterId (boss), Solution2=required kill count
--   Sort=6 (Exchange items):     Solution1=ItemId (before), Solution2=ItemId (after)
--   Sort=7 (Meet NPC):           unused (tracked via Start/EndNPCNumber instead)
--   Sort=8 (Participation event): Solution1=an event/region id
-- Because the target table varies (Items vs Monsters) per-row depending on Sort, Solution1/2 are deliberately
-- NOT foreign-keyed -- a single FK constraint cannot point at two different tables.
--
-- Level is the minimum character level to accept the quest (legacy qLevel); bounds-checked 1..145 in
-- S15_MyShare.cpp, which is exactly LEVEL_INFO's row count (world.Levels has 145 rows, confirmed), so it is
-- FK'd. StartNPCNumber/EndNPCNumber/KeyNpcNumber* are legacy NPC numbers (bounds-checked 1..500/0..500) and
-- FK to world.Npcs, whose real range in this build (1..424) comfortably covers every value seen here.
-- NextIndex chains quests together (qNextIndex); 0 means "end of chain" (3 quests), and one quest (589) points
-- at 689, an excluded padding slot -- functionally identical to "end of chain" -- so both cases store NULL,
-- never a literal 0 or a dangling reference (0/689 are not valid QuestId values).
CREATE TABLE world.Quests
(
    QuestId          INT      NOT NULL,
    Subject          NVARCHAR(51) NOT NULL, -- qSubject[51] buffer size
    Category         TINYINT  NOT NULL,     -- bounds-checked 1..4; no further semantic breakdown recovered
    Step             SMALLINT NOT NULL,     -- bounds-checked 1..1000; real data range 1..207
    Level            SMALLINT NOT NULL,     -- see header comment: FK'd to world.Levels
    Type             TINYINT  NOT NULL,     -- bounds-checked 1..2; no further semantic breakdown recovered
    Sort             TINYINT  NOT NULL,     -- quest-type discriminator, see header comment mapping
    SummonZoneNumber SMALLINT NULL,         -- qSummonInfo[0]; NULL unless Sort=5 (boss summon)
    SummonPosX       INT NULL,              -- qSummonInfo[1]
    SummonPosY       INT NULL,              -- qSummonInfo[2]
    SummonPosZ       INT NULL,              -- qSummonInfo[3]
    StartNPCNumber   INT      NOT NULL,
    KeyNpcNumber1    INT NULL,              -- qKeyNPCNumber[0]; the only slot ever populated in this build
    KeyNpcNumber2    INT NULL,              -- qKeyNPCNumber[1]; always NULL in this build, kept for fidelity
    KeyNpcNumber3    INT NULL,              -- qKeyNPCNumber[2]; always NULL in this build, kept for fidelity
    KeyNpcNumber4    INT NULL,              -- qKeyNPCNumber[3]; always NULL in this build, kept for fidelity
    KeyNpcNumber5    INT NULL,              -- qKeyNPCNumber[4]; always NULL in this build, kept for fidelity
    EndNPCNumber     INT      NOT NULL,
    Solution1        INT NULL,              -- qSolution[0]; polymorphic on Sort, see header comment -- no FK
    Solution2        INT NULL,              -- qSolution[1]; polymorphic on Sort, see header comment -- no FK
    Solution3        INT NULL,              -- qSolution[2]; always NULL in this build, kept for fidelity
    Solution4        INT NULL,              -- qSolution[3]; always NULL in this build, kept for fidelity
    NextIndex        INT NULL,              -- qNextIndex; self-referencing chain, see header comment
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
    CONSTRAINT CK_Quests_Sort CHECK (Sort BETWEEN 1 AND 8)
);
