namespace Fenrir.Tools.DbMigrator.Legacy.Records;

internal sealed record LevelRecord(
    int Index,
    int[] RangeInfo,
    int AttackPower,
    int DefensePower,
    int AttackSuccess,
    int AttackBlock,
    int ElementAttack,
    int Life,
    int Mana);
