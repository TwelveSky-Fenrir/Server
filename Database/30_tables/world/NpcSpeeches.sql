-- Normalized from nSpeech[5][5][51]; SpeechGroup/SpeechIndex mirror the legacy [outer][inner] array indices exactly. Only non-empty lines are stored.
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
