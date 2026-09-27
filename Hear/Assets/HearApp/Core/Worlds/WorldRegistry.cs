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
            new WorldEntry("river-journey", "River of Echoes", "Listen. Explore. Progress.", "RiverJourneyWorld"),
            // Dev-only scratch world for trying out free downloaded 3D environment assets in
            // isolation (human request 2026-09-26: "Zkusme experimentovat s ruznymi svety.") -
            // not a real hearing-trial gameplay loop yet.
            new WorldEntry("experiment", "Experiment", "Listen. Try. See.", "ExperimentWorld"),
            // Dev-only scratch world, same purpose as "experiment" (human request 2026-09-26:
            // "Priprav dalsi svet, nazvi jej Viking Boat. Objekty dodam za chvili") - environment
            // asset arrives separately.
            new WorldEntry("viking-boat", "Viking Boat", "Listen. Try. See.", "VikingBoatWorld"),
            // Dev-only scratch worlds (human request 2026-09-27: "vyzkousej na MACU tyto nove
            // svety: Earth, Mushroooms, Planets. Uvidime, co pujde.") - each a quick pass at a
            // different free asset pack to see what's worth developing further.
            new WorldEntry("planets", "Planets", "Listen. Try. See.", "PlanetsWorld"),
            new WorldEntry("earth", "Earth", "Listen. Try. See.", "EarthWorld"),
            // Dev-only scratch world (human request 2026-09-27, after finding the free "Sleeping
            // Forest" pack's ground textures, glow-capable mushrooms, and firefly effect: "Zaloz
            // toto jako uplne novy svet... snive poeticky nazev" between mushrooms and sound) -
            // superseded the plain low-poly "Mushrooms" world entirely (human request 2026-09-27:
            // "Ted Mushrooms svet muzes smazat, zustaneme jen u Mycenia") - Mushrooms' own model/
            // atlas assets live on under Resources/Worlds/Mushrooms, now reused as imported
            // variety inside this world (see MycMurmurPresentation.ImportedMushroomSpecies).
            new WorldEntry("myc-murmur", "Mycelium Murmur", "Listen. Try. See.", "MycMurmurWorld"),
        };
    }
}
