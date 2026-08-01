namespace Fenrir.Application.Game.Domain.Skills;

public enum SkillValueKind
{
    ManaUse = 1,

    RecoverInfo1 = 2,

    RecoverInfo2 = 3,

    StunAttack = 4,
    StunDefense = 5,
    FastRunSpeed = 6,
    AttackPowerRatio = 7,
    // Read into SkillGradeRowDto.AttackInfo2 but structurally unreachable: the case that
    // applies it (AttackActionValue1==3, Server/ts25zone/S07_MyGame02.cpp:1050-1058 PvP,
    // :1979-1988 PvM) is dead because the enclosing functions' own upstream gate
    // (:736-749 / :1856-1874) only ever accepts {1,2}. Intentionally not consumed here.
    ElementAttackPowerRatio = 8,
    AttackInfo3 = 9,

    RunTime = 10,

    ChargingDamageUp = 11,
    AttackPowerUp = 12,
    DefensePowerUp = 13,
    AttackSuccessUp = 14,
    AttackBlockUp = 15,
    ElementAttackUp = 16,
    ElementDefenseUp = 17,
    AttackSpeedUp = 18,
    RunSpeedUp = 19,

    ShieldLifeUp = 20,

    LuckUp = 21,
    CriticalUp = 22,
    ReturnSuccessUp = 23,
    StunDefenseUp = 24,
    DestroySuccessUp = 25
}
