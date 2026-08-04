using Fenrir.Application.Game.Domain.World.Runtime;
using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Avatars;

public readonly record struct WorldEntryAvatarProjection(
    RuntimeIncarnation Incarnation,
    AvatarInfo AvatarInfo,
    BuffInfo BuffInfo,
    ActionInfo Pose,
    string PartyName);
