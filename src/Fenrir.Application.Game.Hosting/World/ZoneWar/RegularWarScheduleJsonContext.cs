using System.Text.Json.Serialization;
using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Hosting.World.ZoneWar;

[JsonSerializable(typeof(RegularWarScheduleState))]
internal sealed partial class RegularWarScheduleJsonContext : JsonSerializerContext;
