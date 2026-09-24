using System.Collections.Generic;

namespace HearApp.Core.Worlds
{
    /// <summary>Metadata describing a selectable world. The shell only ever shows DisplayName -
    /// implementation technology (2D/2.5D/3D) must never be surfaced to the player.</summary>
    public readonly struct WorldEntry
    {
        public readonly string Id;
        public readonly string DisplayName;
        public readonly string Tagline;
        public readonly string SceneName;

        public WorldEntry(string id, string displayName, string tagline, string sceneName)
        {
            Id = id;
            DisplayName = displayName;
            Tagline = tagline;
            SceneName = sceneName;
        }
    }

    /// <summary>
    /// Stable, hard-coded world order for the carousel. Cold-start active index is randomized by
    /// whoever reads this list (the shell), not by reordering the list itself - order must stay
    /// stable across sessions.
    /// </summary>
    public static class WorldRegistry
    {
        public static readonly IReadOnlyList<WorldEntry> Worlds = new List<WorldEntry>
        {
            new WorldEntry("tide-troubles", "Tide Troubles", "Listen. React. Enjoy.", "TideTroublesWorld"),
            new WorldEntry("paper-garden", "The Paper Garden", "Listen. Watch. Create.", "PaperGardenWorld"),
            new WorldEntry("river-journey", "River Journey", "Listen. Explore. Progress.", "RiverJourneyWorld"),
        };
    }
}
