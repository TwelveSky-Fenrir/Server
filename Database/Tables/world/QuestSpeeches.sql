CREATE TABLE world.QuestSpeeches
(
    QuestId    INT          NOT NULL,
    SpeechKind TINYINT      NOT NULL,
    LineIndex  TINYINT      NOT NULL,
    Text       NVARCHAR(51) NOT NULL,
    Color      INT          NOT NULL,
    CONSTRAINT PK_QuestSpeeches PRIMARY KEY CLUSTERED (QuestId, SpeechKind, LineIndex),
    CONSTRAINT FK_QuestSpeeches_Quest FOREIGN KEY (QuestId) REFERENCES world.Quests (QuestId),
    CONSTRAINT CK_QuestSpeeches_SpeechKind CHECK (SpeechKind BETWEEN 0 AND 9),
    CONSTRAINT CK_QuestSpeeches_LineIndex CHECK (LineIndex BETWEEN 0 AND 14)
);
