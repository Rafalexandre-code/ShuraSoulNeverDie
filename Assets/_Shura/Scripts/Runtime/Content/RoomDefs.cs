namespace Shura
{
    public sealed class RoomDef
    {
        public readonly string name;
        /// <summary>Each wave is a list of enemy ids; the next wave arrives when the current one is dead.</summary>
        public readonly string[][] waves;

        public RoomDef(string name, string[][] waves)
        {
            this.name = name;
            this.waves = waves;
        }
    }

    /// <summary>
    /// Prototype run. Onboarding order: learn to parry on one Ronin, meet the thrust alone,
    /// meet the sweep alone, then combine everything.
    /// </summary>
    public static class RoomDefs
    {
        public static readonly RoomDef[] Prototype =
        {
            new RoomDef("Pátio das Cinzas", new[]
            {
                new[] { "ronin" },
                new[] { "ronin", "ronin" },
            }),
            new RoomDef("Ponte das Lanças", new[]
            {
                new[] { "lanceiro" },
                new[] { "ronin", "lanceiro" },
            }),
            new RoomDef("Templo do Sino", new[]
            {
                new[] { "monge" },
                new[] { "monge", "ronin" },
            }),
            new RoomDef("Portão Shura", new[]
            {
                new[] { "lanceiro", "ronin" },
                new[] { "monge", "lanceiro", "ronin" },
            }),
        };
    }
}
