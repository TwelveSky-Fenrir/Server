CREATE TABLE world.Monsters
(
    MonsterId           INT           NOT NULL,
    Name                NVARCHAR(25)  NOT NULL,
    ChatLine1           NVARCHAR(101) NULL,                                              
    ChatLine2           NVARCHAR(101) NULL,                                              
    Type                TINYINT       NOT NULL,
    SpecialType         TINYINT       NOT NULL,
    DamageType          TINYINT       NOT NULL,
    DataSortNumber      SMALLINT      NOT NULL,                                          
    Size1               SMALLINT      NOT NULL,
    Size2               SMALLINT      NOT NULL,
    Size3               SMALLINT      NOT NULL,
    Size4               SMALLINT      NOT NULL,
    SizeCategory        TINYINT       NOT NULL,
    CheckCollision      TINYINT       NOT NULL,
    TotalHitNum         TINYINT       NOT NULL,
    TotalSkillHitNum    TINYINT       NOT NULL,
    ItemLevel           SMALLINT      NOT NULL,
    MartialItemLevel    SMALLINT      NOT NULL,
    RealLevel           SMALLINT      NOT NULL,
    MartialRealLevel    SMALLINT      NOT NULL,
    GeneralExperience   INT           NOT NULL,
    PatExperience       INT           NOT NULL,
    Life                INT           NOT NULL,                                          
    AttackType          TINYINT       NOT NULL,
    RadiusInfo1         SMALLINT      NOT NULL,
    RadiusInfo2         SMALLINT      NOT NULL,
    WalkSpeed           SMALLINT      NOT NULL,
    RunSpeed            SMALLINT      NOT NULL,
    DeathSpeed          SMALLINT      NOT NULL,
    AttackPower         INT           NOT NULL,                                          
    DefensePower        INT           NOT NULL,
    AttackSuccess       INT           NOT NULL,
    AttackBlock         INT           NOT NULL,
    ElementAttackPower  INT           NOT NULL,
    ElementDefensePower INT           NOT NULL,
    Critical            SMALLINT      NOT NULL,
    FollowInfo1         SMALLINT      NOT NULL,
    FollowInfo2         SMALLINT      NOT NULL,
    SummonTime1         INT           NOT NULL,                                          
    SummonTime2         INT           NOT NULL,
    CONSTRAINT PK_Monsters PRIMARY KEY CLUSTERED (MonsterId),
    CONSTRAINT CK_Monsters_Name CHECK (LEN(Name) <= 24),                                 
    CONSTRAINT CK_Monsters_ChatLines CHECK (                                             
        (ChatLine1 IS NULL OR LEN(ChatLine1) <= 100) AND
        (ChatLine2 IS NULL OR LEN(ChatLine2) <= 100)
        ),
    CONSTRAINT CK_Monsters_Type CHECK (Type BETWEEN 1 AND 15),                           
    CONSTRAINT CK_Monsters_SpecialType CHECK (SpecialType BETWEEN 1 AND 53),             
    CONSTRAINT CK_Monsters_DamageType CHECK (DamageType BETWEEN 1 AND 2),                
    CONSTRAINT CK_Monsters_DataSortNumber CHECK (DataSortNumber BETWEEN 1 AND 10000),    
    CONSTRAINT CK_Monsters_Size1To3 CHECK (                                              
        Size1 BETWEEN 1 AND 1000 AND
        Size2 BETWEEN 1 AND 1000 AND
        Size3 BETWEEN 1 AND 1000
        ),
    CONSTRAINT CK_Monsters_Size4 CHECK (Size4 BETWEEN 0 AND 1000),                       
    CONSTRAINT CK_Monsters_SizeCategory CHECK (SizeCategory BETWEEN 1 AND 4),            
    CONSTRAINT CK_Monsters_CheckCollision CHECK (CheckCollision BETWEEN 1 AND 2),        
    CONSTRAINT CK_Monsters_HitCounts CHECK (                                             
        TotalHitNum BETWEEN 0 AND 3 AND
        TotalSkillHitNum BETWEEN 0 AND 3
        ),
    CONSTRAINT CK_Monsters_ItemLevel CHECK (ItemLevel BETWEEN 1 AND 145),                
    CONSTRAINT CK_Monsters_MartialItemLevel CHECK (MartialItemLevel BETWEEN 0 AND 25),   
    CONSTRAINT CK_Monsters_RealLevel CHECK (RealLevel BETWEEN 1 AND 1000),               
    CONSTRAINT CK_Monsters_MartialRealLevel CHECK (MartialRealLevel BETWEEN 0 AND 1000), 
    CONSTRAINT CK_Monsters_ExperienceRewards CHECK (                                     
        GeneralExperience BETWEEN 0 AND 100000000 AND
        PatExperience BETWEEN 0 AND 100000000
        ),
    CONSTRAINT CK_Monsters_Life CHECK (Life BETWEEN 1 AND 2000000000),                   
    CONSTRAINT CK_Monsters_AttackType CHECK (AttackType BETWEEN 1 AND 6),                
    CONSTRAINT CK_Monsters_RadiusInfo CHECK (                                            
        RadiusInfo1 BETWEEN 0 AND 10000 AND
        RadiusInfo2 BETWEEN 0 AND 10000 AND
        RadiusInfo2 >= RadiusInfo1
        ),
    CONSTRAINT CK_Monsters_MovementSpeeds CHECK (                                        
        WalkSpeed BETWEEN 0 AND 1000 AND
        RunSpeed BETWEEN 0 AND 1000 AND
        DeathSpeed BETWEEN 0 AND 1000
        ),
    CONSTRAINT CK_Monsters_AttackPower CHECK (AttackPower BETWEEN 0 AND 1000000),        
    CONSTRAINT CK_Monsters_CombatStats CHECK (                                           
        DefensePower BETWEEN 0 AND 100000 AND
        AttackSuccess BETWEEN 0 AND 100000 AND
        AttackBlock BETWEEN 0 AND 100000 AND
        ElementAttackPower BETWEEN 0 AND 100000 AND
        ElementDefensePower BETWEEN 0 AND 100000
        ),
    CONSTRAINT CK_Monsters_Critical CHECK (Critical BETWEEN 0 AND 100),                  
    CONSTRAINT CK_Monsters_FollowInfo CHECK (                                            
        FollowInfo1 BETWEEN 0 AND 100 AND
        FollowInfo2 BETWEEN 0 AND 100 AND
        FollowInfo2 >= FollowInfo1
        ),
    CONSTRAINT CK_Monsters_SummonTime CHECK (                                            
        SummonTime1 BETWEEN 1 AND 1000000 AND
        SummonTime2 BETWEEN 1 AND 1000000 AND
        SummonTime2 >= SummonTime1
        )
);
