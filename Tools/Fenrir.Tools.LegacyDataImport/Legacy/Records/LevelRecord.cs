namespace Fenrir.Tools.LegacyDataImport.Legacy.Records;

/// <summary>Faithful in-memory shape of a legacy <c>LEVEL_INFO</c> record (Header/Protocol/STRUCT.h:16-27).</summary>
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
