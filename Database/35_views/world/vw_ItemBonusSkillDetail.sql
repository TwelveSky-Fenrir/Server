-- Readability/tooling view (not called directly by app code -- see 60_permissions/001_roles.sql's "no
-- table/view rights" model; a procedure body could join through this internally, and it's handy for
-- ad-hoc SSMS debugging of "what does item X actually grant"). Resolves world.ItemBonusSkills' two FKs
-- (Items, nullable Skills) into human-readable names instead of raw ids, since the raw child table on
-- its own is opaque without cross-referencing both parent tables by hand.
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
