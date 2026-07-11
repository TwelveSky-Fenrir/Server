namespace Fenrir.Application.Game.Domain.World.Configuration;

public readonly record struct ZoneConfig
{

        public const int NoOwnerTribe = -1;


        public int MinLevel { get; init; }

        public int MaxLevel { get; init; }

        public int OwnerTribe { get; init; }

        public int SecondaryClassification { get; init; }


        public int MaxUser { get; init; }


        public int GeneralExperienceRatio { get; init; }

        public int PetExperienceRatio { get; init; }

        public int MountExperienceRatio { get; init; }

        public int BonusExperienceRatio { get; init; }

        public int TeacherPointRatio { get; init; }

        public int NormalItemDropRatio { get; init; }

        public int RareItemDropRatio { get; init; }

        public int EliteItemDropRatio { get; init; }

        public int MoneyDropRatio { get; init; }

        public int KillOtherTribeAddValue { get; init; }

        public int KillOtherTribeExperienceRatio { get; init; }

        public int SpecialZone38ExperienceRatio { get; init; }

        public int SpecialZone38MoneyRatio { get; init; }

        public int SpecialZone175ExperienceRatio { get; init; }

        public int SpecialZone175MoneyRatio { get; init; }

        public int RegularWarZone49Window { get; init; }

        public int RegularWarZone51Window { get; init; }

        public int RegularWarZone53Window { get; init; }

        public int WarCountdownTime { get; init; }

        public int WarExperienceRatio { get; init; }

        public int WarPvpRatio { get; init; }

        public int WarMoneyRatio { get; init; }

        public static ZoneConfig Unconfigured { get; } = new() { OwnerTribe = NoOwnerTribe };
}
