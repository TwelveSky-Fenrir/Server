-- DELETE-then-INSERT, not MERGE/UPDATE (same idiom as runtime.usp_SessionTicket_Create). Natively
-- compiled -- see game.GuildNotices for why this path is memory-optimized.
CREATE PROCEDURE game.usp_GuildNotice_Set @GuildId     INT,
    @NoticeIndex TINYINT,
    @Text        NVARCHAR(50)
WITH NATIVE_COMPILATION, SCHEMABINDING
AS
BEGIN ATOMIC
WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
DELETE
FROM game.GuildNotices
WHERE GuildId = @GuildId
  AND NoticeIndex = @NoticeIndex;

INSERT INTO game.GuildNotices (GuildId, NoticeIndex, Text, UpdatedAtUtc)
VALUES (@GuildId, @NoticeIndex, @Text, SYSUTCDATETIME());
END;
