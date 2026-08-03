CREATE TABLE world.StarterKitHotkeys
(
    PreviousTribe TINYINT NOT NULL,
    Page          TINYINT NOT NULL,
    KeyIndex      TINYINT NOT NULL,
    Sort          INT     NOT NULL,
    Value1        INT     NOT NULL,
    Value2        INT     NOT NULL,
    CONSTRAINT PK_StarterKitHotkeys PRIMARY KEY CLUSTERED (PreviousTribe, Page, KeyIndex),
    CONSTRAINT CK_StarterKitHotkeys_PreviousTribe CHECK (PreviousTribe BETWEEN 0 AND 2),
    CONSTRAINT CK_StarterKitHotkeys_Page CHECK (Page BETWEEN 0 AND 2),
    CONSTRAINT CK_StarterKitHotkeys_KeyIndex CHECK (KeyIndex BETWEEN 0 AND 13)
);
