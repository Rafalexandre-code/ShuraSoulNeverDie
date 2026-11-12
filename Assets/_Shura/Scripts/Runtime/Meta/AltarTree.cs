using UnityEngine;

namespace Shura
{
    /// <summary>The end of an Altar branch: a rule, not a number (design doc §7.3).</summary>
    public sealed class AltarNode
    {
        public string id;
        public string name;
        public string text;
        public int ecos;
        public int fragments;
    }

    public sealed class AltarBranch
    {
        public string name;
        public string kanji;
        public string[] upgrades;
        public AltarNode node;
        /// <summary>Levels in this branch before its end can be opened.</summary>
        public int required;
        public Color color;
    }

    /// <summary>
    /// The Altar das Almas as a tree: the eight upgrades are the roots of three branches
    /// (Corpo, Lâmina, Mente). Every level takes one point and every rule node two, and the
    /// points are limited, so a soul builds a profile instead of buying everything.
    /// </summary>
    public static class AltarTree
    {
        public const string Diamond = "diamante";
        public const string SecondSwap = "segunda_troca";
        public const string Clairvoyance = "clarividencia";
        public const int NodePoints = 2;
        public const int BasePoints = 12;
        public const int MaxPoints = 22;

        public static readonly AltarBranch[] Branches =
        {
            new AltarBranch
            {
                name = "Corpo", kanji = "体", upgrades = new[] { "vigor", "postura", "folego" }, required = 6,
                color = ColorUtil.Hex("#C8553D"),
                node = new AltarNode
                {
                    id = Diamond, name = "Corpo de Diamante", ecos = 150, fragments = 3,
                    text = "O primeiro ferimento de cada sala é ignorado.",
                },
            },
            new AltarBranch
            {
                name = "Lâmina", kanji = "刃", upgrades = new[] { "lamina", "olhar" }, required = 3,
                color = ColorUtil.Hex("#7FA7C9"),
                node = new AltarNode
                {
                    id = SecondSwap, name = "Segunda Troca", ecos = 180, fragments = 4,
                    text = "Leve 3 armas: a troca gira entre elas (A › B › C › A).",
                },
            },
            new AltarBranch
            {
                name = "Mente", kanji = "心", upgrades = new[] { "meditacao", "despertar", "visao" }, required = 3,
                color = ColorUtil.Hex("#B48EDB"),
                node = new AltarNode
                {
                    id = Clairvoyance, name = "Clarividência", ecos = 150, fragments = 3,
                    text = "O mapa revela as salas inteiras: inimigos, guardiões e clima de cada porta.",
                },
            },
        };

        public static AltarNode Node(string id)
        {
            foreach (AltarBranch b in Branches)
            {
                if (b.node.id == id) return b.node;
            }
            return null;
        }

        public static AltarBranch BranchOf(string upgradeOrNode)
        {
            foreach (AltarBranch b in Branches)
            {
                if (b.node.id == upgradeOrNode) return b;
                foreach (string u in b.upgrades)
                {
                    if (u == upgradeOrNode) return b;
                }
            }
            return null;
        }

        /// <summary>Points grow with the lives lived: 12, then one more every 3 runs, up to 22.</summary>
        public static int PointsLimit(MetaProgress meta) => Mathf.Min(MaxPoints, BasePoints + meta.runs / 3);

        public static int PointsUsed(MetaProgress meta)
        {
            int used = 0;
            foreach (MetaUpgradeDef def in MetaUpgrades.All) used += meta.GetLevel(def.id);
            if (meta.nodes != null) used += meta.nodes.Count * NodePoints;
            return used;
        }

        public static int PointsFree(MetaProgress meta) => PointsLimit(meta) - PointsUsed(meta);

        public static int BranchLevels(MetaProgress meta, AltarBranch b)
        {
            int n = 0;
            foreach (string u in b.upgrades) n += meta.GetLevel(u);
            return n;
        }

        public static bool NodeReady(MetaProgress meta, AltarBranch b) => BranchLevels(meta, b) >= b.required;

        public static bool CanOpen(MetaProgress meta, AltarBranch b)
        {
            return !meta.HasNode(b.node.id) && NodeReady(meta, b) && meta.ecos >= b.node.ecos &&
                   meta.fragments >= b.node.fragments && PointsFree(meta) >= NodePoints;
        }

        public static bool TryOpen(MetaProgress meta, AltarBranch b)
        {
            if (!CanOpen(meta, b)) return false;
            meta.ecos -= b.node.ecos;
            meta.fragments -= b.node.fragments;
            meta.nodes.Add(b.node.id);
            return true;
        }

        public static void ApplyNodes(MetaProgress meta, PlayerModifiers m)
        {
            if (meta.HasNode(Diamond)) m.ignoreFirstWound = true;
            if (meta.HasNode(SecondSwap)) m.thirdWeapon = true;
            if (meta.HasNode(Clairvoyance)) m.clairvoyance = true;
        }
    }
}
