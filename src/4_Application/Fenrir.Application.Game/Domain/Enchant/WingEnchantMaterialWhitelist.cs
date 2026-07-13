using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Enchant;

public static class WingEnchantMaterialWhitelist
{

        public const int GuaranteedSuccessScrollItemId = 826;

        public const int ProtectedMaterialItemId = 8106;

    public const int ProtectedMaterialFailureResultCode = 9;

    public const int ProtectedMaterialEnchantValue = 1;

        public const int WingEnchantCpCost = 50;

        public static readonly FrozenDictionary<int, int> StandardWingMaterials = new Dictionary<int, int>
    {
        [695] = 1,
        [696] = 2,
        [698] = 3,
        [2397] = 4,
        [826] = 40
    }.ToFrozenDictionary();
}
