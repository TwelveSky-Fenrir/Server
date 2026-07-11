using System.Collections.Immutable;

namespace Fenrir.Application.Game.Stats.Context;

public readonly record struct CosmeticContext(
    ImmutableArray<int> RuneItemIds = default,
    ImmutableArray<int> RuneStatValues = default,
    int CostumeNumber = 0,
    int CostumeState = 0,
    int StellarCoreNumber = 0,
    CostumeBaseStatBlock CostumeValue = default,
    int CostumeEnchantCs = 0);
