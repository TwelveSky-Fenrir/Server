-- Resolves world.ItemBonusSkills' Item/Skill FKs into names for debugging.
CREATE VIEW world.vw_ItemBonusSkillDetail
AS
SELECT bs.ItemId,
       i.Name AS ItemName,
       bs.SlotIndex,
       bs.SkillId,
       s.Name AS SkillName,
       bs.Value
FROM world.ItemBonusSkills AS bs
         INNER JOIN world.Items AS i ON i.ItemId = bs.ItemId
         LEFT JOIN world.Skills AS s ON s.SkillId = bs.SkillId;
