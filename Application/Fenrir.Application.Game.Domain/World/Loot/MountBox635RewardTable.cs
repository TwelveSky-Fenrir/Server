using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.Loot;

public static class MountBox635RewardTable
{
    public const int BoxItemId = 635;

    public const int Tiger3 = 1307;
    public const int Pig3 = 1308;
    public const int Deer3 = 1309;
    public const int Bear3 = 1315;
    public const int Cat3 = 1319;
    public const int Bull3 = 1322;
    public const int Wolf3 = 1325;
    public const int Lion3 = 1328;

    public static readonly ImmutableArray<int> RewardItemIds =
        [Tiger3, Pig3, Deer3, Bear3, Cat3, Bull3, Wolf3, Lion3];

    public static BoxRewardSpec CreateSpec()
    {
        return BoxRewardSpec.Uniform(BoxItemId, RewardItemIds);
    }
}
