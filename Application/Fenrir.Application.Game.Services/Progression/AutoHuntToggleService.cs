using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Progression;

public sealed class AutoHuntToggleService(ICharacterRepository characters, ILogger<AutoHuntToggleService> logger)
    : IAutoHuntToggleService
{

        private const byte WeaponSlot = 7;

    public async ValueTask<AutoHuntToggleResult> ToggleAsync(int characterId, Zone zone, PlayerRuntimeState state,
        AutoHuntToggleRequest packet, CancellationToken cancellationToken)
    {
        if (packet.Sort is not (0 or 1))
            return new AutoHuntToggleResult(true, false);

        var validation = AutoHuntConfigValidator.Validate(packet.AutoHunt);
        if (!validation.IsValid)
        {
            logger.LogWarning(
                "Auto-hunt config rejected for character {CharacterId}: {Rejection} -- aborting session",
                characterId, validation.Rejection);
            return new AutoHuntToggleResult(true, false);
        }

        if (packet.Sort == 1)
        {
            var hasWeapon = state.Inventory.GetSlot(ContainerMatrix.Equipment, WeaponSlot) is not null;
            var hasAttackSkill = packet.AutoHunt.AttackType.Length >= 3 &&
                                 (packet.AutoHunt.AttackType[0] != 0 || packet.AutoHunt.AttackType[2] != 0);

            var enableBlocked = AutoHuntEnableGate.IsEnableBlocked(zone.MapId) ||
                                 AutoHuntBattleZoneEligibilityCatalog.IsBlocked(zone.MapId, state.CombinedLevel,
                                     state.RebirthCount);
            if (enableBlocked || !hasWeapon || !hasAttackSkill)
            {
                logger.LogDebug(
                    "Auto-hunt enable rejected for character {CharacterId} on map {MapId}: enableBlocked={EnableBlocked} hasWeapon={HasWeapon} hasAttackSkill={HasAttackSkill}",
                    characterId, zone.MapId, enableBlocked, hasWeapon, hasAttackSkill);
                return new AutoHuntToggleResult(true, false);
            }
        }

        var enabled = packet.Sort == 1;
        var configBytes = new byte[AutoHunt.WireSize];
        packet.AutoHunt.Write(configBytes);

        await characters.SetAutoHuntAsync(characterId, enabled, configBytes, cancellationToken);

        state.AutoHuntEnabled = enabled;
        state.AutoHuntConfig = packet.AutoHunt;

        return new AutoHuntToggleResult(false, enabled);
    }
}
