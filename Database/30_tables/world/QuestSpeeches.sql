-- Normalized from QUEST_INFO's ten speech-block fields (qStartSpeech/qHurrySpeech/qProcessSpeech1-5/qSuccessSpeech/qFailureSpeech/qCallSpeech).
-- SpeechKind is an invented discriminator (no such field in legacy data); order must match the C# generator: 0=Start,1=Hurry,2-6=Process1-5,7=Success,8=Failure,9=Call.
CREATE TABLE world.QuestSpeeches
(
    QuestId    INT     NOT NULL,
    SpeechKind TINYINT NOT NULL,
    LineIndex  TINYINT NOT NULL,
    Text       NVARCHAR(51) NOT NULL,
    Color      INT     NOT NULL,
    CONSTRAINT PK_QuestSpeeches PRIMARY KEY CLUSTERED (QuestId, SpeechKind, LineIndex),
    CONSTRAINT FK_QuestSpeeches_Quest FOREIGN KEY (QuestId) REFERENCES world.Quests (QuestId),
    CONSTRAINT CK_QuestSpeeches_SpeechKind CHECK (SpeechKind BETWEEN 0 AND 9),
    CONSTRAINT CK_QuestSpeeches_LineIndex CHECK (LineIndex BETWEEN 0 AND 14)
);
