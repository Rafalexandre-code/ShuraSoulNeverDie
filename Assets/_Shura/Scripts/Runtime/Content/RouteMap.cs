using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>What waits behind a door of the route map (design doc §6.2).</summary>
    public enum RoomKind
    {
        Combat,
        /// <summary>One against one, no minions, opens with an Iai standoff; the foe's technique is guaranteed.</summary>
        Duel,
        /// <summary>Waves with a condition (only parry, no damage, against the clock); success drops a rare item or better.</summary>
        Challenge,
        /// <summary>Two factions already fighting each other; the reward is a double choice.</summary>
        Dispute,
        Shrine,
        Merchant,
        Smith,
        /// <summary>A narrative choice (§6.7).</summary>
        Event,
        /// <summary>An excellent item with a curse.</summary>
        CursedTreasure,
        Guardian,
        /// <summary>Every 25 rooms: the Gate of the Shuradō and the next Kalpa (§6.6).</summary>
        Gate,
    }

    /// <summary>The reward symbol painted on a combat door.</summary>
    public enum RewardHint
    {
        Any,
        Relic,
        Blessing,
        Item,
        Technique,
    }

    public enum ChallengeKind
    {
        None,
        /// <summary>"Só parry": no dodging.</summary>
        ParryOnly,
        /// <summary>Clear it without being hit.</summary>
        NoHit,
        /// <summary>Clear it in 45 seconds.</summary>
        Timed,
    }

    public sealed class RouteNode
    {
        public int depth;
        public int index;
        /// <summary>0..2 across the scroll; doors link to the next layer's doors within one column.</summary>
        public int column;
        public RoomKind kind;
        public RewardHint hint;
        public ChallengeKind challenge;
        public int eventId = -1;

        public bool IsEncounter => RouteMap.IsEncounter(kind);
    }

    /// <summary>
    /// The route map: each depth is a layer of 1–3 doors, generated from the run's seed (the
    /// same seed always draws the same map). A door links to the doors of the next layer whose
    /// column is at most one away, so every door can always be reached and always leads on.
    /// </summary>
    public static class RouteMap
    {
        /// <summary>Layers revealed beyond the next one.</summary>
        public const int Lookahead = 3;
        public const int KalpaLength = 25;
        public const int ChallengeSeconds = 45;

        public static bool IsEncounter(RoomKind k) =>
            k == RoomKind.Shrine || k == RoomKind.Merchant || k == RoomKind.Smith || k == RoomKind.Event ||
            k == RoomKind.CursedTreasure || k == RoomKind.Gate;

        public static bool IsGateDepth(int depth) => depth > 0 && depth % KalpaLength == 0;

        struct Weighted
        {
            public RoomKind kind;
            public int weight;
            public int minDepth;
        }

        static readonly Weighted[] Table =
        {
            new Weighted { kind = RoomKind.Combat, weight = 40, minDepth = 1 },
            new Weighted { kind = RoomKind.Duel, weight = 10, minDepth = 2 },
            new Weighted { kind = RoomKind.Challenge, weight = 10, minDepth = 2 },
            new Weighted { kind = RoomKind.Dispute, weight = 8, minDepth = 4 },
            new Weighted { kind = RoomKind.Shrine, weight = 7, minDepth = 2 },
            new Weighted { kind = RoomKind.Merchant, weight = 8, minDepth = 2 },
            new Weighted { kind = RoomKind.Smith, weight = 5, minDepth = 3 },
            new Weighted { kind = RoomKind.Event, weight = 12, minDepth = 2 },
            new Weighted { kind = RoomKind.CursedTreasure, weight = 4, minDepth = 3 },
        };

        static bool IsFight(RoomKind k) => k == RoomKind.Combat || k == RoomKind.Duel || k == RoomKind.Challenge || k == RoomKind.Dispute;

        public static List<RouteNode> Layer(ulong seed, int depth)
        {
            var nodes = new List<RouteNode>();
            if (depth <= 0)
            {
                nodes.Add(new RouteNode { depth = 0, column = 1, kind = RoomKind.Combat });
                return nodes;
            }
            var rng = new SeededRandom(RoomGenerator.Mix(seed, depth + 7000));
            if (RoomGenerator.IsGuardianDepth(depth))
            {
                nodes.Add(new RouteNode { depth = depth, column = 1, kind = RoomKind.Guardian, hint = RewardHint.Item });
                return nodes;
            }
            if (IsGateDepth(depth))
            {
                nodes.Add(new RouteNode { depth = depth, column = 1, kind = RoomKind.Gate });
                return nodes;
            }

            int count = depth == 1 ? 2 : rng.NextFloat() < 0.6f ? 3 : 2;
            var used = new HashSet<RoomKind>();
            for (int i = 0; i < count; i++)
            {
                RoomKind kind = depth == 1 ? RoomKind.Combat : Pick(rng, depth, used);
                if (kind != RoomKind.Combat) used.Add(kind);
                var node = new RouteNode
                {
                    depth = depth, index = i, kind = kind,
                    column = count == 3 ? i : count == 2 ? i * 2 : 1,
                };
                nodes.Add(node);
            }
            // At least one door is a fight.
            bool anyFight = false;
            foreach (RouteNode n in nodes) anyFight |= IsFight(n.kind);
            if (!anyFight) nodes[rng.Range(0, nodes.Count)].kind = RoomKind.Combat;

            var hints = new List<RewardHint> { RewardHint.Relic, RewardHint.Blessing, RewardHint.Item };
            foreach (RouteNode n in nodes)
            {
                switch (n.kind)
                {
                    case RoomKind.Duel:
                        n.hint = RewardHint.Technique;
                        break;
                    case RoomKind.Challenge:
                        n.hint = RewardHint.Item;
                        n.challenge = (ChallengeKind)rng.Range(1, 4);
                        break;
                    case RoomKind.Combat:
                    case RoomKind.Dispute:
                    {
                        int k = rng.Range(0, hints.Count);
                        n.hint = hints[k];
                        if (hints.Count > 1) hints.RemoveAt(k);
                        break;
                    }
                    case RoomKind.Event:
                        n.eventId = (int)((seed + (ulong)depth * 5UL + (ulong)n.index) % (ulong)RunEvents.Count);
                        break;
                }
            }
            return nodes;
        }

        static RoomKind Pick(SeededRandom rng, int depth, HashSet<RoomKind> used)
        {
            int total = 0;
            foreach (Weighted w in Table)
            {
                if (depth >= w.minDepth && !used.Contains(w.kind)) total += w.weight;
            }
            int roll = rng.Range(0, Mathf.Max(1, total));
            foreach (Weighted w in Table)
            {
                if (depth < w.minDepth || used.Contains(w.kind)) continue;
                roll -= w.weight;
                if (roll < 0) return w.kind;
            }
            return RoomKind.Combat;
        }

        /// <summary>Doors of the next layer reachable from this one.</summary>
        public static List<RouteNode> Next(ulong seed, RouteNode from)
        {
            List<RouteNode> layer = Layer(seed, from.depth + 1);
            var list = new List<RouteNode>();
            foreach (RouteNode n in layer)
            {
                if (Mathf.Abs(n.column - from.column) <= 1) list.Add(n);
            }
            if (list.Count == 0) list.AddRange(layer);
            return list;
        }

        /// <summary>The door taken when nobody chooses (tests, dev jumps): the first fight of the layer.</summary>
        public static RouteNode Default(ulong seed, int depth)
        {
            List<RouteNode> layer = Layer(seed, depth);
            foreach (RouteNode n in layer)
            {
                if (n.kind == RoomKind.Combat) return n;
            }
            // Guardians and gates stand alone; otherwise a plain fight behind the middle door.
            if (layer.Count == 1) return layer[0];
            return new RouteNode { depth = depth, column = 1, kind = RoomKind.Combat };
        }

        // ── Names and symbols ─────────────────────────────────────────────────────────

        static readonly string[] KindNames =
        {
            "Combate", "Duelo", "Desafio", "Disputa", "Santuário", "Mercador", "Ferreiro Errante", "Evento",
            "Tesouro Amaldiçoado", "Guardião", "Portão do Shuradō",
        };

        static readonly string[] KindGlyphs = { "戦", "決", "試", "争", "社", "商", "鍛", "縁", "呪", "主", "門" };

        static readonly string[] KindTexts =
        {
            "Ondas de inimigos.",
            "Um contra um, sem lacaios, começa em impasse. A técnica dele é garantida.",
            "Uma condição. Cumprida, um item raro ou melhor.",
            "Duas facções já lutando entre si. Recompensa: escolha dupla.",
            "Meditar, tratar ferimentos, mudar a guarda. Cura e limpeza.",
            "Compre com Mon: itens, bandagens, papéis, goles.",
            "Fundir, gravar ou afiar os seus itens.",
            "Uma escolha.",
            "Um item excelente com uma maldição.",
            "O Guardião do bioma.",
            "Desça ao próximo Kalpa: uma regra nova para sempre.",
        };

        static readonly string[] HintNames = { "", "Relíquia", "Bênção", "Item", "Técnica" };

        static readonly string[] ChallengeNames = { "", "Só aparar (sem esquivar)", "Sem tomar dano", "Contra o tempo (45 s)" };

        public static string KindName(RoomKind k) => KindNames[(int)k];
        public static string KindGlyph(RoomKind k) => KindGlyphs[(int)k];
        public static string KindText(RoomKind k) => KindTexts[(int)k];
        public static string HintName(RewardHint h) => HintNames[(int)h];
        public static string ChallengeName(ChallengeKind c) => ChallengeNames[(int)c];

        public static Color KindColor(RoomKind k)
        {
            switch (k)
            {
                case RoomKind.Duel: return ColorUtil.Hex("#E9C46A");
                case RoomKind.Challenge: return ColorUtil.Hex("#F4A261");
                case RoomKind.Dispute: return ColorUtil.Hex("#E76F51");
                case RoomKind.Shrine: return ColorUtil.Hex("#9BD39B");
                case RoomKind.Merchant: return ColorUtil.Hex("#F2C14E");
                case RoomKind.Smith: return ColorUtil.Hex("#C8C2B8");
                case RoomKind.Event: return ColorUtil.Hex("#6FC3DF");
                case RoomKind.CursedTreasure: return ColorUtil.Hex("#B06CFF");
                case RoomKind.Guardian: return ColorUtil.Hex("#FF5A3C");
                case RoomKind.Gate: return ColorUtil.Hex("#D7263D");
                default: return ColorUtil.Hex("#EDE6D6");
            }
        }

        /// <summary>"Combate · Relíquia", "Desafio · Sem tomar dano".</summary>
        public static string Describe(RouteNode n)
        {
            string s = KindName(n.kind);
            if (n.kind == RoomKind.Challenge) return s + " · " + ChallengeName(n.challenge);
            if (n.kind == RoomKind.Event && n.eventId >= 0) return s + " · " + RunEvents.Title(n.eventId);
            if (n.hint != RewardHint.Any && (n.kind == RoomKind.Combat || n.kind == RoomKind.Dispute)) return s + " · " + HintName(n.hint);
            return s;
        }
    }
}
