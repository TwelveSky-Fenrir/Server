using Fenrir.Application.Game.World;
using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Handlers.Progression.Services;

/// <summary>Outcome of <see cref="IAutoPotionThresholdService.ApplyAsync" />: <see cref="Aborted" /> means the caller must disconnect the session.</summary>
public readonly record struct AutoPotionThresholdResult(bool Aborted);

public interface IAutoPotionThresholdService
{
    ValueTask<AutoPotionThresholdResult> ApplyAsync(int characterId, PlayerRuntimeState state, int value01,
        int value02, CancellationToken cancellationToken);
}

/// <summary>Business logic extracted from <c>AutoPotionThresholdHandler</c> (CZ_CHANGE_AUTO_INFO, opcode 86).</summary>
public sealed class AutoPotionThresholdService(ICharacterRepository characters) : IAutoPotionThresholdService
{
    public async ValueTask<AutoPotionThresholdResult> ApplyAsync(int characterId, PlayerRuntimeState state,
        int value01, int value02, CancellationToken cancellationToken)
    {
        if (value01 is < 0 or > 5 || value02 is < 0 or > 5)
            return new AutoPotionThresholdResult(true);

        var lifeRatio = (byte)value01;
        var manaRatio = (byte)value02;

        await characters.SetAutoPotionThresholdAsync(characterId, lifeRatio, manaRatio, cancellationToken);

        // Written directly, not EconomyActionLock-guarded: own-character scalar, no item/money involved.
        state.AutoLifeRatio = lifeRatio;
        state.AutoManaRatio = manaRatio;

        return new AutoPotionThresholdResult(false);
    }
}
