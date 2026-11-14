using System.Collections.Generic;

namespace Shura
{
    /// <summary>The systems that open one layer at a time (design doc §7.7).</summary>
    public enum Unlockable
    {
        Seals,
        Gourd,
        RouteMap,
        Kamae,
        Tools,
        Temple,
        Reactions,
        ShuraMeter,
        Fusions,
        Oaths,
        Lineage,
        Rivals,
    }

    /// <summary>
    /// With this many systems, a new soul should not meet all of them at once. Each layer
    /// opens after a number of finished runs (1 = the second run already has it). The dojo
    /// always teaches everything, the test robot plays with everything, and the settings
    /// can open everything at once for experienced players.
    /// </summary>
    public static class Unlocks
    {
        public static int RunsFor(Unlockable f)
        {
            switch (f)
            {
                case Unlockable.Seals:
                case Unlockable.Gourd:
                case Unlockable.RouteMap:
                    return 1;
                case Unlockable.Kamae:
                case Unlockable.Tools:
                case Unlockable.Temple:
                    return 3;
                case Unlockable.Reactions:
                case Unlockable.ShuraMeter:
                case Unlockable.Rivals:
                    return 6;
                default:
                    return 10;
            }
        }

        public static bool Open(int runs, Unlockable f) => runs >= RunsFor(f);

        public static string Name(Unlockable f)
        {
            switch (f)
            {
                case Unlockable.Seals: return "Selos de Ki";
                case Unlockable.Gourd: return "Cabaça";
                case Unlockable.RouteMap: return "Mapa de rotas";
                case Unlockable.Kamae: return "Guardas (kamae)";
                case Unlockable.Tools: return "Ferramentas";
                case Unlockable.Temple: return "Templo e Forja";
                case Unlockable.Reactions: return "Reações elementais";
                case Unlockable.ShuraMeter: return "Medidor de Shura";
                case Unlockable.Fusions: return "Fusões";
                case Unlockable.Oaths: return "Juramentos";
                case Unlockable.Rivals: return "Rivais";
                default: return "Linhagem";
            }
        }

        /// <summary>What opened when the count of runs went from <paramref name="before"/> to <paramref name="after"/>.</summary>
        public static List<Unlockable> Opened(int before, int after)
        {
            var list = new List<Unlockable>();
            foreach (Unlockable f in System.Enum.GetValues(typeof(Unlockable)))
            {
                if (!Open(before, f) && Open(after, f)) list.Add(f);
            }
            return list;
        }

        public static string Names(List<Unlockable> features)
        {
            var names = new List<string>();
            foreach (Unlockable f in features) names.Add(Name(f));
            return string.Join(", ", names);
        }
    }
}
