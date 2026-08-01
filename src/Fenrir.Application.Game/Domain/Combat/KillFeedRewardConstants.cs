namespace Fenrir.Application.Game.Domain.Combat;

public static class KillFeedRewardConstants
{
    public const int FfaWarPointPerKill = 2;

    public const int FfaBloodPointPerKill = 2;

    // Bareme unique cp[] = { 100, 50, 25 }, double seulement quand mCheckZone267TypeServer
    // (Server/ts25zone/S07_MyGame02.cpp:188-194), arme pour les seules cartes 267/268/269/250
    // (Server/ts25zone/S07_MyGame01.cpp:1199-1225). La carte FFA 335 n'a pas de bareme propre.
    public const int Top1ContributionPoints = 100;

    public const int Top2ContributionPoints = 50;

    public const int Top3ContributionPoints = 25;

    public const int Zone267ContributionPointMultiplier = 2;

    public static readonly TimeSpan FfaAntiFarmCooldown = TimeSpan.FromMinutes(3);
}
