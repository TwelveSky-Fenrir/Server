-- Normalized from nSpeech[MAX_NPC_SPEECH_NUM1=5][MAX_NPC_SPEECH_NUM2=5][MAX_NPC_SPEECH_LENGTH=51]
-- (Header/Protocol/STRUCT.h:213-234): SpeechGroup/SpeechIndex mirror the legacy [outer][inner] array
-- indexing exactly (both 0-based, matching NpcRecord.Speech[outer][inner]). Only non-empty lines are
-- stored -- genuinely sparse even within NPCs that use speech at all: of the 131 real NPCs, 130 have at
-- least one non-empty line, but only ~53% of the 25 slots those 130 NPCs could use are actually
-- populated (1720 rows out of a 3275-slot ceiling for real NPCs; 12500 for the full 500-slot legacy
-- array) -- a genuine child table, not 25 columns, is the right shape here (unlike Npcs.Size1-3).
-- Text length: legacy buffer is 51 bytes; longest real value observed is 50 chars, so NVARCHAR(51) has a
-- one-byte margin matching the legacy NUL terminator convention rather than being an arbitrary round number.
CREATE TABLE world.NpcSpeeches
(
    NpcId       INT     NOT NULL,
    SpeechGroup TINYINT NOT NULL,
    SpeechIndex TINYINT NOT NULL,
    Text        NVARCHAR(51) NOT NULL,
    CONSTRAINT PK_NpcSpeeches PRIMARY KEY CLUSTERED (NpcId, SpeechGroup, SpeechIndex),
    CONSTRAINT CK_NpcSpeeches_SpeechGroup CHECK (SpeechGroup BETWEEN 0 AND 4),
    CONSTRAINT CK_NpcSpeeches_SpeechIndex CHECK (SpeechIndex BETWEEN 0 AND 4),
    CONSTRAINT FK_NpcSpeeches_Npcs FOREIGN KEY (NpcId) REFERENCES world.Npcs (NpcId)
);
