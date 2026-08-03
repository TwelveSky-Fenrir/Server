CREATE PROCEDURE game.usp_GuildNotice_Set @GuildId INT,
                                          @NoticeIndex TINYINT,
                                          @Text NVARCHAR(50)
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM game.GuildNotices
    WHERE GuildId = @GuildId
      AND NoticeIndex = @NoticeIndex;

    INSERT INTO game.GuildNotices (GuildId, NoticeIndex, Text, UpdatedAtUtc)
    VALUES (@GuildId, @NoticeIndex, @Text, SYSUTCDATETIME());
END;
