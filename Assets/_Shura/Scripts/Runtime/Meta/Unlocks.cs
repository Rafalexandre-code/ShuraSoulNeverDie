using System.Collections.Generic;

namespace Shura
{
    /// <summary>The systems that open one layer at a time (design doc §7.7).</summary>
    public enum Feature
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
    }

    /// <summary>
    /// With this many systems, a new soul should not meet all of them at once. Each layer
    /// opens after a number of finished runs (1 = the second run already has it). The dojo
    /// always teaches everything, the test robot plays with everything, and the settings
    /// can open everything at once for experienced players.
    /// </summary>
    public static class Unlocks
    {
        public static int RunsFor(Feature f)
        {
            switch (f)
            {
                case Feature.Seals:
                case Feature.Gourd:
                case Feature.RouteMap:
                    return 1;
                case Feature.Kamae:
                case Feature.Tools:
                case Feature.Temple:
                    return 3;
                case Feature.Reactions:
                case Feature.ShuraMeter:
                    return 6;
                default:
                    return 10;
            }
        }

        public static bool Open(int runs, Feature f) => runs >= RunsFor(f);

        public static string Name(Feature f)
        {
            switch (f)
            {
                case Feature.Seals: return "Selos de Ki";
                case Feature.Gourd: return "Cabaça";
                case Feature.RouteMap: return "Mapa de rotas";
                case Feature.Kamae: return "Guardas (kamae)";
                case Feature.Tools: return "Ferramentas";
                case Feature.Temple: return "Templo e Forja";
                case Feature.Reactions: return "Reações elementais";
                case Feature.ShuraMeter: return "Medidor de Shura";
                case Feature.Fusions: return "Fusões";
                case Feature.Oaths: return "Juramentos";
                default: return "Linhagem";
            }
        }

        /// <summary>What opened when the count of runs went from <paramref name="before"/> to <paramref name="after"/>.</summary>
        public static List<Feature> Opened(int before, int after)
        {
            var list = new List<Feature>();
            foreach (Feature f in System.Enum.GetValues(typeof(Feature)))
            {
                if (!Open(before, f) && Open(after, f)) list.Add(f);
            }
            return list;
        }

        public static string Names(List<Feature> features)
        {
            var names = new List<string>();
            foreach (Feature f in features) names.Add(Name(f));
            return string.Join(", ", names);
        }
    }
}
