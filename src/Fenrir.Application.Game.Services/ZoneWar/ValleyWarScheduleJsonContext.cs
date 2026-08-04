using System.Text.Json.Serialization;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Services.ZoneWar;

[JsonSerializable(typeof(ValleyWarScheduleState))]
internal sealed partial class ValleyWarScheduleJsonContext : JsonSerializerContext;
