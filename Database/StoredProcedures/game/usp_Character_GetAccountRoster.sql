CREATE PROCEDURE game.usp_Character_GetAccountRoster @AccountId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CharacterId,
           Slot,
           Name,
           Tribe,
           PreviousTribe,
           Gender,
           HeadType,
           FaceType,
           Level,
           Level2,
           Halo,
           RebirthCount,
           ContributionPoints,
           SkillPoints,
           EatLifePotion,
           EatManaPotion,
           EatStrPotion,
           EatDexPotion,
           EatElePotion,
           PetGrowth,
           PetActivity,
           MapId,
           PosX,
           PosY,
           PosZ,
           Life,
           Mana,
           VisibleState,
           SpecialState,
           CostumeIndex
    FROM game.Characters
    WHERE AccountId = @AccountId
    ORDER BY Slot;

    SELECT ci.CharacterId,
           ci.Container,
           ci.Slot,
           ci.ItemId,
           CAST(ci.Quantity AS INT) AS Quantity,
           ci.Enchant,
           ci.Combine,
           ci.Refine,
           ci.Socket,
           ci.SocketGem1,
           ci.SocketGem2,
           ci.SocketGem3,
           ci.ExpireDate,
           ci.Serial
    FROM game.CharacterItems AS ci
             JOIN game.Characters AS c ON c.CharacterId = ci.CharacterId
    WHERE c.AccountId = @AccountId
    ORDER BY ci.CharacterId, ci.Container, ci.Slot;

    SELECT pb.CharacterId,
           pb.Slot,
           pb.ItemId
    FROM game.CharacterPetBag AS pb
             JOIN game.Characters AS c ON c.CharacterId = pb.CharacterId
    WHERE c.AccountId = @AccountId
    ORDER BY pb.CharacterId, pb.Slot;

    SELECT cc.CharacterId,
           cc.Slot,
           cc.ItemId
    FROM game.CharacterCostumes AS cc
             JOIN game.Characters AS c ON c.CharacterId = cc.CharacterId
    WHERE c.AccountId = @AccountId
    ORDER BY cc.CharacterId, cc.Slot;
END;
