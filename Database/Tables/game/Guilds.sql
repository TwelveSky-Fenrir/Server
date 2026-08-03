CREATE TABLE game.Guilds
(
    GuildId           INT IDENTITY (1,1) NOT NULL,
    Name              NVARCHAR(12)       NOT NULL,                  
    Grade             INT                NOT NULL
        CONSTRAINT DF_Guilds_Grade DEFAULT 0,                       
    MasterCharacterId INT                NULL,                      
    Points            INT                NOT NULL
        CONSTRAINT DF_Guilds_Points DEFAULT 0,                      
    BuffType          INT                NOT NULL
        CONSTRAINT DF_Guilds_BuffType DEFAULT 0,                    
    BuffState         INT                NOT NULL
        CONSTRAINT DF_Guilds_BuffState DEFAULT 0,                   
    BuffTime          INT                NOT NULL
        CONSTRAINT DF_Guilds_BuffTime DEFAULT 0,                    
    BuffTimeForDiff   BIGINT             NOT NULL
        CONSTRAINT DF_Guilds_BuffTimeForDiff DEFAULT 0,             
    Logo              INT                NOT NULL
        CONSTRAINT DF_Guilds_Logo DEFAULT 0,                        
    CreatedAtUtc      DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Guilds_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
    UpdatedAtUtc      DATETIME2(3)       NOT NULL
        CONSTRAINT DF_Guilds_UpdatedAtUtc DEFAULT SYSUTCDATETIME(), 
    CONSTRAINT PK_Guilds PRIMARY KEY CLUSTERED (GuildId),
    CONSTRAINT UQ_Guilds_Name UNIQUE (Name),
    CONSTRAINT FK_Guilds_MasterCharacter FOREIGN KEY (MasterCharacterId) REFERENCES game.Characters (CharacterId),
    INDEX IX_Guilds_MasterCharacter NONCLUSTERED (MasterCharacterId)
);
