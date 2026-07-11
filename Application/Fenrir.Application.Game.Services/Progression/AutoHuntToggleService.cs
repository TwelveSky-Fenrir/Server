using Fenrir.Application.Game.Abstractions.Progression;
using Fenrir.Application.Game.Domain.Inventory;
using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Services.Progression;

/// <summary>Business logic extracted from <c>AutoHuntToggleHandler</c> (CZ_AUTO_CONFIG_SEND, opcode 99).</summary>
public sealed class AutoHuntToggleService(ICharacterRepository characters, ILogger<AutoHuntToggleService> logger)
    : IAutoHuntToggleService
{
    /// <summary>FEQUIP_TYPE::EWEAPON slot index.</summary>
    private const byte WeaponSlot = 7;

    public async ValueTask<AutoHuntToggleResult> ToggleAsync(int characterId, Zone zone, PlayerRuntimeState state,
        AutoHuntToggleRequest packet, CancellationToken cancellationToken)
    {
        if (packet.Sort is not (0 or 1))
            return new AutoHuntToggleResult(true, false);

        // Security hardening (finding: the legacy 112-byte AUTO_HUNT blob is copied into stored server state
        // verbatim with no field validation, S04_MyWork02.cpp:13612). Validate every field server-side BEFORE
        // storing it -- for both the enable and disable paths, since both copy the blob into stored state. A
        // malformed blob is a hard disconnect, matching legacy's own malformed-input handling; the validator is
        // conservative (rejects only never-legitimate values) so a well-behaved client is never wrongly kicked.
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
            // Legacy's `!(!A && !B)` is `A || B`: either attack-type slot alone is a valid setup.
            var hasAttackSkill = packet.AutoHunt.AttackType.Length >= 3 &&
                                 (packet.AutoHunt.AttackType[0] != 0 || packet.AutoHunt.AttackType[2] != 0);

            // The cited fixed-number blocked-zone set (38 / 319-323 / the 20 "zone 241 type" numbers) --
            // AutoHuntEnableGate, Terms A/B -- plus the level-and-rebirth-gated battle-zone eligibility term
            // (S18_MyZoneInfo.cpp:440-508) -- AutoHuntBattleZoneEligibilityCatalog, Term C.
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
