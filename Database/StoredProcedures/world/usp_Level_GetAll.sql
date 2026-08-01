CREATE PROCEDURE world.usp_Level_GetAll
AS
BEGIN
    SET
        NOCOUNT ON;

    SELECT Level,
           ExpRangeMin,
           ExpRangeMax,
           RangeInfo3,
           AttackPower,
           DefensePower,
           AttackSuccess,
           AttackBlock,
           ElementAttack,
           Life,
           Mana
    FROM world.Levels
    ORDER BY Level;
END;
