namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{
    /// <summary>
    ///     <c>mParty_Buff_Act</c> (Server/ts25zone/H07_MyGame.h:1002) -- the single per-character byte of session
    ///     state that carries the Formation party-buff cast/confirm handshake across packets between resets.
    ///     Advanced by <see cref="Zone.AdvanceCasterPartyBuffMarker" /> when this character casts a party-buff
    ///     Formation skill (76/77/79/81) with action sort 64 (arm) or 65 (confirm), and read by the
    ///     formation-buff broadcast (which requires <see cref="PartyBuffAction.Done" />). Reset to
    ///     <see cref="PartyBuffAction.None" /> on character (re-)initialization and on zone/state-change events
    ///     (teleport, zone move, death-type transitions -- S07_MyGame03.cpp:9600-9621, S07_MyGame04.cpp:95,1587),
    ///     so the two-step handshake never survives those events. Defaults to <see cref="PartyBuffAction.None" />,
    ///     matching the legacy zero-init at avatar registration -- a fresh <see cref="PlayerRuntimeState" /> per
    ///     zone entry therefore already satisfies the "(re-)init resets to none" requirement without an explicit
    ///     call; only in-place transitions that reuse the same state object need
    ///     <see cref="Zone.ResetPartyBuffMarker" />.
    /// </summary>
    public PartyBuffAction PartyBuffAct { get; set; } = PartyBuffAction.None;
}
