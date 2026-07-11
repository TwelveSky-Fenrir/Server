namespace Fenrir.Application.Game.Domain.Gm;

public readonly record struct GmSelfExperienceGrantZoneCommand(
    int CharacterId,
    int Mode,
    int Magnitude,
    TaskCompletionSource? Applied = null);
