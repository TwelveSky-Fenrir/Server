using Fenrir.Application.Game.Simulation;
using Fenrir.Application.Game.Skills;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Network.Sessions;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     CZ_CONTINUE_SKILL_USE_SEND (op95). Sort=1 starts auto-buff channeling (mana-gated, broadcasts the
///     resulting avatar action); Sort=2 is the per-tick buff-apply pulse -- a documented, wire-faithful no-op
///     here (the legacy itself never replies or disconnects for Sort=2 either; see
///     <see cref="AutoBuffActivationResolver" />'s remarks for what's out of scope). Any other Sort disconnects.
/// </summary>
public sealed class ContinueSkillUseHandler : IInlinePacketHandler<ContinueSkillUseRequest>
{
    public void Handle(in ContinueSkillUseRequest packet, IPacketSession session)
    {
        var zoneSession = (ZoneClientSession)session;
        if (zoneSession.CurrentZone is not Zone zone)
            return;

        var characterId = zoneSession.CharacterId!.Value;
        if (!zone.TryGetPlayer(characterId, out var state) || state is null)
            return;

        var context = new AutoBuffActivationResolver.Context(state.AutoBuffTime, state.ActionSort, state.Mana);
        var result = AutoBuffActivationResolver.Resolve(packet.Sort, in context, GameDate.Today());

        switch (result.Kind)
        {
            case AutoBuffActivationResolver.ResultKind.NoReply:
            case AutoBuffActivationResolver.ResultKind.Tick:
                return;

            case AutoBuffActivationResolver.ResultKind.Disconnect:
                zoneSession.Abort(DisconnectReason.Faulted);
                return;

            case AutoBuffActivationResolver.ResultKind.Activate:
                session.Send(new AutoBuffActivationResponse { Value = 0 });
                zone.PostAutoBuffCommand(new AutoBuffZoneCommand(characterId,
                    ManaAfterActivation: result.ManaAfterActivation,
                    ActionSort: AutoBuffActivationResolver.ChannelingActionSort, Broadcast: true));
                return;
        }
    }
}
