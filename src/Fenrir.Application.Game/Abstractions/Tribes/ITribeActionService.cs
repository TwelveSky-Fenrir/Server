using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Abstractions.Tribes;

public readonly record struct TribeActionOutcome(bool Aborted, int Result)
{
    public static readonly TribeActionOutcome Abort = new(true, 0);

    public static TribeActionOutcome Ok(int result = 0)
    {
        return new TribeActionOutcome(false, result);
    }
}

public interface ITribeActionService
{
    public ValueTask<TribeActionOutcome> ResetStatsAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    public ValueTask<TribeActionOutcome> AppointSubMasterAsync(Zone zone, PlayerRuntimeState state, byte[] data,
        CancellationToken ct);

    public ValueTask<TribeActionOutcome> RemoveSubMasterAsync(Zone zone, PlayerRuntimeState state, byte[] data,
        CancellationToken ct);

    public ValueTask<TribeActionOutcome> UseTribeWeaponAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    public TribeActionOutcome ValidateTribeSkill(PlayerRuntimeState state, byte[] data);

    public ValueTask<TribeActionOutcome> PurchaseTitleAsync(Zone zone, PlayerRuntimeState state, int characterId,
        byte[] data, CancellationToken ct);

    public ValueTask<TribeActionOutcome> HaloEnchantAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    public ValueTask<TribeActionOutcome> ClaimLevelBonusAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    public ValueTask<TribeActionOutcome> SetOrnamentAsync(Zone zone, PlayerRuntimeState state, int characterId, bool on,
        CancellationToken ct);

    public ValueTask<TribeActionOutcome> RedeemMapScrollAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    public ValueTask<TribeActionOutcome> RedeemAlertCharmAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct);

    public ValueTask<TribeActionOutcome> UseTowerScrollAsync(Zone zone, PlayerRuntimeState state, int characterId,
        CancellationToken ct);
}
