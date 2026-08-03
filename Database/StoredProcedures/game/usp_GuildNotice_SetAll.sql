CREATE PROCEDURE game.usp_GuildNotice_SetAll @GuildId INT,
                                             @Text0 NVARCHAR(50),
                                             @Text1 NVARCHAR(50),
                                             @Text2 NVARCHAR(50),
                                             @Text3 NVARCHAR(50)
    WITH NATIVE_COMPILATION , SCHEMABINDING
AS
BEGIN
    ATOMIC
    WITH (TRANSACTION ISOLATION LEVEL = SNAPSHOT, LANGUAGE = N'us_english')
    DELETE
    FROM game.GuildNotices
    WHERE GuildId = @GuildId;

    INSERT INTO game.GuildNotices (GuildId, NoticeIndex, Text, UpdatedAtUtc)
    VALUES (@GuildId, 0, @Text0, SYSUTCDATETIME());

    INSERT INTO game.GuildNotices (GuildId, NoticeIndex, Text, UpdatedAtUtc)
    VALUES (@GuildId, 1, @Text1, SYSUTCDATETIME());

    INSERT INTO game.GuildNotices (GuildId, NoticeIndex, Text, UpdatedAtUtc)
    VALUES (@GuildId, 2, @Text2, SYSUTCDATETIME());

    INSERT INTO game.GuildNotices (GuildId, NoticeIndex, Text, UpdatedAtUtc)
    VALUES (@GuildId, 3, @Text3, SYSUTCDATETIME());
END;
