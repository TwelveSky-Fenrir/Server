-- Normalized from QUEST_INFO's ten repeated "speech block" fields (qStartSpeech/qHurrySpeech/
-- qProcessSpeech1..5/qSuccessSpeech/qFailureSpeech/qCallSpeech), each a char[15][51] dialogue-line array with
-- a matching int[15] per-line color array -- 10 x 15 x 2 = 300 columns if transliterated literally, which the
-- task brief explicitly forbids ("il ne faut pas betement copier la logique du code Legacy"). Only 18,742 of
-- the theoretical 1000 x 10 x 15 = 150,000 line slots are ever non-empty (still only 18,742 of 688 x 10 x 15 =
-- 103,200 once padding quest slots are excluded) -- the vast majority of blocks/lines are unused, so a child
-- table keyed on (QuestId, SpeechKind, LineIndex) with one row per populated line is the right shape.
-- SpeechKind is our own invented convention (QUEST_INFO has no such discriminator field) -- the declaration
-- order of the ten legacy speech blocks, fixed here so the C# generator and this table always agree:
--   0=Start, 1=Hurry, 2=Process1, 3=Process2, 4=Process3, 5=Process4, 6=Process5, 7=Success, 8=Failure, 9=Call
-- Text is NVARCHAR(51) matching the legacy char[51] buffer size exactly (max real content observed is 50
-- chars, the 51st byte is the NUL terminator). Color is the legacy per-line int color code (small values
-- like 1/2/27/30 -- a client-side palette index, not an RGB triple).
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
