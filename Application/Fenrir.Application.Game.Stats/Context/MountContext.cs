using System.Collections.Immutable;

namespace Fenrir.Application.Game.Stats.Context;

public readonly record struct MountContext(
    int AnimalNumber = 0,
    int AnimalGrade = 0,
    bool AbsorbActive = false,
    int AbsorbValue = 0,
    ImmutableArray<int> RuntimeAttributes = default);
