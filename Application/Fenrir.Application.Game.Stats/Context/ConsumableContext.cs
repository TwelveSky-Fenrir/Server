namespace Fenrir.Application.Game.Stats.Context;

/// <summary>
///     Consumable-derived BASE-layer stat inputs the legacy <c>MyFactor</c> reads that the current
///     equipment-only <see cref="StatCalculator" /> surface has no channel for: the five lifetime stat/elixir
///     potion counters plus the two pill/boost flags that together apply a flat max-life multiplier. Every
///     field defaults to a neutral/zero value, so a <c>default</c> instance reproduces "no consumable
///     contribution". All inputs here feed the BASE cache (recompute-on-discrete-event).
/// </summary>
/// <remarks>
///     Introduced by workstream B1 as a pure plumbing seam: threaded through
///     <see cref="StatCalculator.ComputeBaseStats" /> and its base getters but not read by any formula yet
///     (each contributes 0). Réf. legacy input surface: the five <c>Eat*Potion</c> counters and their
///     per-event-cap gating inside the elixir function <c>Server/Header/Protocol/MyFactor.cpp:560,562,581-731</c>;
///     the HP-boost / warrior-pill flags applying the 1.2x max-life multiplier
///     <c>MyFactor.cpp:1941-1943</c>.
///     <para>
///         The two flags (<paramref name="HpBoostActive" />/<paramref name="WarriorPillActive" />) are NOT in
///         the source finding's enumerated five-counter list -- the behavior contract flags them as an
///         additional consumable-derived base input that legacy reads at <c>MyFactor.cpp:1941-1943</c>, carried
///         here rather than silently omitted. Open question for <c>cpp-zone-gameplay-analyst</c>: was that
///         omission deliberate scoping?
///     </para>
/// </remarks>
/// <param name="EatLifePotion">aEatLifePotion -- lifetime Life/HP stat-potion headroom consumed.</param>
/// <param name="EatManaPotion">aEatManaPotion -- lifetime Mana/MP stat-potion headroom consumed.</param>
/// <param name="EatStrPotion">aEatStrPotion -- lifetime Strength stat-potion headroom consumed.</param>
/// <param name="EatDexPotion">aEatDexPotion -- lifetime Dexterity (AGI) stat-potion headroom consumed.</param>
/// <param name="EatElePotion">
///     aEatElePotion -- lifetime Elemental stat-potion headroom consumed (packs two sub-values, see
///     <c>PlayerRuntimeState.EatElePotion</c>'s own remarks).
/// </param>
/// <param name="HpBoostActive">HP-boost flag contributing to the 1.2x max-life multiplier (BASE, zone-gated).</param>
/// <param name="WarriorPillActive">Warrior-pill flag contributing to the 1.2x max-life multiplier (BASE, zone-gated).</param>
/// <param name="MaxPotionEventNum">
///     mMaxPotionEventNum -- the per-avatar potion-event tier (10/20/30) consumed by
///     <c>StatCalculator.FourGuildElixirContribution.cs</c>'s <c>*ElixirContributionWithOverride</c> engine;
///     0 = not set (falls to the raw floor). <b>WORKSTREAM B7-fourguild-eventtier-source (wave14) traced every
///     write site</b> for the legacy field this backs (<c>MyFactor::mMaxPotionEventNum</c>,
///     <c>Server/Header/Protocol/MyFactor.h:37</c>): it is a plain per-session <c>MyFactor</c>-instance
///     member, not an <c>AVATAR_INFO</c>/persisted-avatar field at all, its constructor default is 0
///     (<c>MyFactor.cpp:301-303</c>), its only setter (<c>SetAvatar</c>, <c>MyFactor.cpp:320-329</c>) is called
///     exactly once in the whole codebase and passes the instance's own current value straight back into
///     itself (<c>AVATAR_OBJECT::SetBasicAbilityFromEquip</c>, <c>Server/ts25zone/S07_MyGame04.cpp:158-170</c>),
///     and <c>AVATAR_OBJECT::Clear()</c> resets it to -1 (<c>S07_MyGame04.cpp:60,119-120</c>) -- so this value
///     is provably never anything but 0 or -1 in the compiled legacy build. There is therefore no
///     <c>PlayerRuntimeState</c> field to thread this from: adding one would require Fenrir to invent its own
///     new activation mechanism (an admin tool, a GM command, a new event feature) for a system legacy itself
///     never turns on -- a product decision, not a missing-citation gap. Stays 0 (raw floor) until that
///     decision is made.
/// </param>
/// <param name="EventTribe">
///     The event-tribe context supplied at the recompute that further gates the capped counter path; 0 = none.
///     Backs <c>MyFactor::mMaxPotionEventTribe</c> (<c>MyFactor.h:36</c>) -- the exact same "never anything but
///     its 0/-1 default" finding as <see cref="MaxPotionEventNum" /> applies here too (same constructor/
///     setter/Clear() trail). Note this field is also a <c>byte</c> and so cannot represent the legacy -1
///     "disabled" sentinel the way the <c>*ElixirContributionWithOverride</c> methods' own <c>eventTribe</c>
///     parameter does (default <c>0</c> here is itself a valid tribe id) -- <c>EquipmentService.AssembleStatContexts</c>
///     deliberately does NOT read this field into that parameter for that reason (see its own remarks); it
///     stays plumbed here only so a future signed/nullable replacement has a documented home.
/// </param>
public readonly record struct ConsumableContext(
    int EatLifePotion = 0,
    int EatManaPotion = 0,
    int EatStrPotion = 0,
    int EatDexPotion = 0,
    int EatElePotion = 0,
    bool HpBoostActive = false,
    bool WarriorPillActive = false,
    int MaxPotionEventNum = 0,
    byte EventTribe = 0);
