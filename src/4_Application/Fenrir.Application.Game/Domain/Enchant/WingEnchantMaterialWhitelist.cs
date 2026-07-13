using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Enchant;

public static class WingEnchantMaterialWhitelist
{
    public const int GuaranteedSuccessScrollItemId = 826;

    public const int GuaranteedSuccessScrollAmount = 50;

    public const int ProtectedMaterialItemId = 8106;

    public const int ProtectedMaterialFailureResultCode = 9;

    public const int ProtectedMaterialEnchantValue = 1;

    public const int SiblingWithSharedEnchantValueItemId = 695;

    public const int WingEnchantCpCost = 50;

    public static readonly FrozenSet<int> ClassWhitelist =
        new[] { 695, 696, 698, 826, 2387, 2392, 2397, 8106 }.ToFrozenSet();

    public static readonly FrozenSet<int> Gate2MissingDisconnects = new[] { 2387, 2392 }.ToFrozenSet();

    public static readonly FrozenSet<int> WhitelistedAmountNotCited = new[] { 696, 698, 2397 }.ToFrozenSet();
}
