using Fenrir.Application.Game.Domain.Mounts;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Data.WriteBehind;

namespace Fenrir.Application.Game.Hosting.World;

public sealed class ProgressWriteBehindHost(ZoneRegistry zones, ICharacterRepository characters)
{
    private const DirtyFlags ProgressFlags = DirtyFlags.Vitals | DirtyFlags.Progression;

    public async ValueTask<IReadOnlySet<int>> FlushAsync(IReadOnlyDictionary<int, DirtyFlags> dirty,
        CancellationToken ct)
    {
        var rows = new List<CharacterProgressTvp>(dirty.Count);
        var claimed = new HashSet<int>();

        foreach (var (characterId, flags) in dirty)
        {
            if ((flags & ProgressFlags) == 0)
                continue;

            if (!zones.TryGetPlayer(characterId, out var state))
                continue;

            rows.Add(new CharacterProgressTvp(characterId, state.FlushSequence, state.Level, state.Level2,
                state.Experience, state.Life, state.MaxLife, state.Mana, state.MaxMana, state.StatVit,
                state.StatStr, state.StatInt, state.StatDex, state.StatPoints, state.SkillPoints,
                state.ContributionPoints, state.Exp2, state.RebirthCount, state.EatLifePotion, state.EatManaPotion,
                state.EatStrPotion, state.EatDexPotion, state.EatElePotion, state.DropItemTime,
                state.M15PetLuckyBoxPity,
                state.MountGarage[MountPersistenceCodec.PersistedGarageSlot],
                MountPersistenceCodec.EncodeExpActivity(state.MountActivity, state.MountAccumulatedExp),
                MountPersistenceCodec.EncodePower(state.MountRolledAttributes),
                state.AnimalIndex, state.AnimalTime));

            claimed.Add(characterId);
        }

        await characters.PersistProgressAsync(rows, ct).ConfigureAwait(false);

        return claimed;
    }
}
