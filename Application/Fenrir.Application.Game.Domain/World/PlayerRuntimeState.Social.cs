using System.Collections.Concurrent;

namespace Fenrir.Application.Game.Domain.World;

public partial class PlayerRuntimeState
{

        public int? GuildId { get; set; }

    public string GuildName { get; set; } = "";

    public byte GuildRoleDb { get; set; }

        public string GuildCallName { get; set; } = "";

        public bool GuildBuffActive { get; set; }

        public bool GuildBuffActiveMirror { get; set; }

        public byte TribeRole { get; set; }

        public ConcurrentDictionary<byte, int> Friends { get; } = new();

        public int? TeacherCharacterId { get; set; }

        public int? StudentCharacterId { get; set; }
}
