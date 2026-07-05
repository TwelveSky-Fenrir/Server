using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Services.Progression;

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
