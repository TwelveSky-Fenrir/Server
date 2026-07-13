namespace Fenrir.Application.Game.Stats.Context;

public readonly record struct MountContext(
    int AnimalNumber = 0,
    bool AbsorbActive = false,
    int AbsorbValue = 0,
    int RolledPower = 0,
    int Activity = 0);
