using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Something to use in an encounter room: a shrine to meditate at, a merchant's ware, a
    /// choice of an event, the torii that leads on. Stand on it and hold Interact (E / X /
    /// the browser's E) for half a second. Most are personal — each duelist buys, prays or
    /// chooses for themselves; a few (fights, the Kalpa gate) decide for the whole party.
    /// </summary>
    public sealed class Offering
    {
        public string Title;
        public string Detail;
        public int Price;
        public Color Color = Color.white;
        public Vector2 Position;
        /// <summary>Tile art: sheet and tile rectangle (column, row from the top-left, size in tiles).</summary>
        public string Sheet;
        public RectInt Tile;
        /// <summary>Or a character standing there (an NPC).</summary>
        public string Character;
        /// <summary>Each duelist can use it once (otherwise the first use spends it for all).</summary>
        public bool PerPlayer = true;
        /// <summary>Can be used again and again (the shrine's guard).</summary>
        public bool Repeatable;
        /// <summary>The torii: everyone standing on it moves the party on.</summary>
        public bool Exit;
        public Func<PlayerController, bool> Act;
        public readonly HashSet<PlayerController> UsedBy = new HashSet<PlayerController>();
        /// <summary>Said no to this duelist (nothing to purify, an empty bag…) until they step off it.</summary>
        public readonly HashSet<PlayerController> Refused = new HashSet<PlayerController>();
        public readonly Dictionary<PlayerController, float> Progress = new Dictionary<PlayerController, float>();
        public bool Spent;
        public Transform Root;

        public bool AvailableTo(PlayerController p) => !Spent && !Refused.Contains(p) && (Repeatable || !PerPlayer || !UsedBy.Contains(p));

        public float MaxProgress
        {
            get
            {
                float m = 0f;
                foreach (float v in Progress.Values) m = Mathf.Max(m, v);
                return m;
            }
        }
    }

    public sealed partial class World
    {
        public const float OfferingHoldSeconds = 0.5f;
        public const float ExitHoldSeconds = 0.8f;
        const float OfferingReach = 1.1f;

        public readonly List<Offering> Offerings = new List<Offering>();
        /// <summary>The banner of an encounter room (the event's name and its words).</summary>
        public string EncounterTitle { get; private set; }
        public string EncounterText { get; private set; }
        Transform encounterRoot;
        List<KalpaRule> gateOffer;

        void ClearEncounter()
        {
            Offerings.Clear();
            EncounterTitle = null;
            EncounterText = null;
            if (encounterRoot != null) UnityEngine.Object.Destroy(encounterRoot.gameObject);
            encounterRoot = null;
        }

        // ── Building the room ─────────────────────────────────────────────────────────

        void SetupEncounter(RoomDef room)
        {
            ClearEncounter();
            encounterRoot = new GameObject("Encounter").transform;
            encounterRoot.SetParent(root, false);
            var rng = new SeededRandom(RoomGenerator.Mix(Run.Seed, room.depth + 5000));
            EncounterTitle = RouteMap.KindName(room.kind);
            EncounterText = RouteMap.KindText(room.kind);
            switch (room.kind)
            {
                case RoomKind.Shrine: BuildShrine(); break;
                case RoomKind.Merchant: BuildMerchant(rng, room.depth); break;
                case RoomKind.Smith: BuildSmith(); break;
                case RoomKind.CursedTreasure: BuildTreasure(rng, room.depth); break;
                case RoomKind.Gate: BuildGate(rng); break;
                case RoomKind.Event: BuildEvent(room.eventId < 0 ? 0 : room.eventId); break;
            }

            var exit = new Offering
            {
                Title = "Seguir caminho", Detail = "Todos sobre o torii para seguir.", Exit = true, PerPlayer = false,
                Color = Config.palette.gold, Sheet = "tiles_house", Tile = new RectInt(0, 5, 3, 2),
            };
            Offerings.Add(exit);
            PlaceOfferings();
        }

        Offering Add(string title, string detail, int price, Color color, Func<PlayerController, bool> act, bool perPlayer = true)
        {
            var o = new Offering { Title = title, Detail = detail, Price = price, Color = color, Act = act, PerPlayer = perPlayer };
            Offerings.Add(o);
            return o;
        }

        static Offering Tile(Offering o, string sheet, int x, int y, int w, int h)
        {
            o.Sheet = sheet;
            o.Tile = new RectInt(x, y, w, h);
            return o;
        }

        /// <summary>A row across the room; the torii stands at the far right.</summary>
        void PlaceOfferings()
        {
            Rect b = Layout.FloorBounds;
            var items = new List<Offering>();
            foreach (Offering o in Offerings)
            {
                if (!o.Exit) items.Add(o);
            }
            float left = b.xMin + 4.5f, right = b.xMax - 5f;
            for (int i = 0; i < items.Count; i++)
            {
                float t = items.Count == 1 ? 0.5f : i / (float)(items.Count - 1);
                float y = b.center.y + (items.Count > 4 ? (i % 2 == 0 ? 1.2f : -1.4f) : 0.2f);
                items[i].Position = Layout.NearestFloor(new Vector2(Mathf.Lerp(left, right, t), y), 0.5f);
            }
            foreach (Offering o in Offerings)
            {
                if (o.Exit) o.Position = Layout.NearestFloor(new Vector2(b.xMax - 2f, b.center.y), 0.6f);
                BuildOfferingView(o);
            }
        }

        void BuildOfferingView(Offering o)
        {
            var go = new GameObject("Offering");
            go.transform.SetParent(encounterRoot, false);
            go.transform.position = o.Position;
            o.Root = go.transform;
            int order = -Mathf.RoundToInt(o.Position.y * 20f);
            var ring = SpriteUtil.Make("Ring", go.transform, ShapeLibrary.Get(Shape.Ring), o.Color.WithAlpha(0.45f), Sorting.Ground + 2);
            ring.transform.localScale = Vector3.one * (o.Exit ? 3.2f : 1.6f);
            Sprite art = null;
            if (!string.IsNullOrEmpty(o.Character))
            {
                CharacterArt c = PixelArt.Character(o.Character);
                if (c != null) art = c.idle[0];
            }
            else if (!string.IsNullOrEmpty(o.Sheet))
            {
                art = PixelArt.Region(o.Sheet, o.Tile.x, o.Tile.y, o.Tile.width, o.Tile.height, new Vector2(0.5f, 0f));
            }
            if (art != null)
            {
                var s = SpriteUtil.Make("Art", go.transform, art, Color.white, order);
                s.transform.localPosition = new Vector3(0f, o.Exit ? -0.6f : -0.3f, 0f);
            }
        }

        // ── Per tick ──────────────────────────────────────────────────────────────────

        /// <summary>Stand on it and hold Interact; the torii needs everyone standing on it.</summary>
        void TickEncounter()
        {
            if (Offerings.Count == 0 || !roomActive || RoomCleared) return;
            bool fighting = ActiveEnemyCount > 0;
            foreach (Offering o in Offerings)
            {
                if (o.Exit)
                {
                    TickExit(o, fighting);
                    continue;
                }
                if (o.Spent) continue;
                foreach (PlayerController p in Players)
                {
                    bool near = p.IsAlive && Vector2.Distance(p.Position, o.Position) <= OfferingReach;
                    // Stepping off lets a refused duelist try again.
                    if (!near) o.Refused.Remove(p);
                    bool holding = near && !fighting && o.AvailableTo(p) && p.Input != null && p.Input.InteractHeld;
                    o.Progress.TryGetValue(p, out float t);
                    t = holding ? t + SimClock.Dt : 0f;
                    if (t >= OfferingHoldSeconds)
                    {
                        t = 0f;
                        Use(o, p);
                    }
                    if (t > 0f) o.Progress[p] = t;
                    else o.Progress.Remove(p);
                    if (o.Spent) break;
                }
            }
        }

        void TickExit(Offering o, bool fighting)
        {
            int alive = 0, on = 0;
            foreach (PlayerController p in Players)
            {
                if (!p.IsAlive) continue;
                alive++;
                if (Vector2.Distance(p.Position, o.Position) <= 1.8f) on++;
            }
            o.Progress.TryGetValue(Host, out float t);
            t = !fighting && alive > 0 && on == alive ? t + SimClock.Dt : 0f;
            o.Progress[Host] = t;
            if (t < ExitHoldSeconds) return;
            o.Spent = true;
            FinishEncounter();
        }

        void FinishEncounter()
        {
            RoomCleared = true;
            roomActive = false;
            Coordinator.Reset();
            ClearProjectiles();
            ClearBlasts();
            CollectAllPickups();
            RoomClearedEvent?.Invoke();
        }

        void Use(Offering o, PlayerController p)
        {
            PlayerBuild b = p.Build;
            if (b == null) return;
            if (o.Price > 0 && b.Mon < o.Price)
            {
                Fx.Popup(string.Format(Strings.PopupNoMon, o.Price), p.Position + Vector2.up * 1.2f, Config.palette.danger);
                o.Refused.Add(p);
                return;
            }
            PlayerController before = actor;
            actor = p;
            bool done = o.Act == null || o.Act(p);
            actor = before;
            if (!done)
            {
                o.Refused.Add(p);
                return;
            }
            if (o.Price > 0)
            {
                b.Mon -= o.Price;
                Fx.Popup("−" + o.Price + " Mon", p.Position + Vector2.up * 1.6f, Config.palette.gold);
            }
            o.UsedBy.Add(p);
            if (!o.PerPlayer) o.Spent = true;
            Fx.Emit(FxType.TechniqueAbsorbed, o.Position, Vector2.up, 0.6f, o.Color);
        }

        void Say(PlayerController p, string text, Color c) => Fx.Popup(text, p.Position + Vector2.up * 1.4f, c);

        // ── Shrine ───────────────────────────────────────────────────────────────────

        void BuildShrine()
        {
            foreach (PlayerController p in Players) p.Heal(p.MaxHealth * 0.25f);
            Color green = Config.palette.blessing;
            Tile(Add("Meditar", "−20 de Shura e o fôlego volta inteiro.", 0, green, p =>
            {
                p.CalmShura(20f);
                p.RestoreBreath();
                Say(p, "MENTE CALMA", green);
                return true;
            }), "tiles_house", 3, 15, 2, 2);
            Tile(Add("Tratar ferimentos", "Cura todos os ferimentos, +1 bandagem e um pouco de vida.", 0, green, p =>
            {
                p.ClearWounds();
                p.Build.Bandages++;
                p.Heal(p.MaxHealth * 0.2f);
                Say(p, "ENFAIXADO", green);
                return true;
            }), "tiles_element", 4, 1, 1, 1);
            Tile(Add("Purificar", "Desfaz a maldição que você carrega.", 0, green, p =>
            {
                if (p.Build.CurseRooms <= 0)
                {
                    Say(p, "nenhuma maldição", Config.palette.uiDim);
                    return false;
                }
                p.Build.CurseRooms = 0;
                p.Build.Curse = RunCurse.None;
                p.Build.RecomputeModifiers();
                p.ApplyStats(false);
                Say(p, "PURIFICADO", green);
                return true;
            }), "tiles_element", 5, 2, 1, 1);
            Offering guard = Tile(Add("Guarda padrão", "Troca a guarda com que você começa cada sala.", 0, Config.palette.playerAccent, p =>
            {
                p.Build.DefaultStance = (Kamae)(((int)p.Build.DefaultStance + 1) % 4);
                p.SetStance(p.Build.DefaultStance);
                return true;
            }), "tiles_house", 0, 15, 1, 2);
            guard.Repeatable = true;
        }

        // ── Merchant ─────────────────────────────────────────────────────────────────

        public static int ItemPrice(Rarity r) => Mathf.Min(400, 30 + 25 * (int)r * ((int)r + 1));

        void BuildMerchant(SeededRandom rng, int depth)
        {
            Color gold = Config.palette.gold;
            for (int i = 0; i < 3; i++)
            {
                ulong seed = rng.NextULong();
                Item sample = ItemGenerator.Roll(new SeededRandom(seed), depth + 2, false);
                Offering o = Add(sample.Name + " (" + Rarities.Name(sample.rarity) + ")", sample.Gain, ItemPrice(sample.rarity), Rarities.Color(sample.rarity), p =>
                {
                    // Everyone buys their own copy of the ware.
                    Item item = ItemGenerator.Roll(new SeededRandom(seed), depth + 2, false);
                    bool equipped = p.Build.AddItem(item);
                    p.ApplyStats(false);
                    Fx.ItemCollected(item, equipped, p.Position);
                    return true;
                });
                Tile(o, "tiles_house", 16 + i, 13, 1, 1);
            }
            Tile(Add("Bandagem", "+1 bandagem (trata um ferimento).", 20, gold, p =>
            {
                p.Build.Bandages++;
                return true;
            }), "tiles_element", 13, 2, 1, 1);
            Tile(Add("Papéis espirituais ×3", "Para as ferramentas.", 15, gold, p =>
            {
                p.Build.SpiritPapers += 3;
                return true;
            }), "tiles_element", 11, 1, 1, 1);
            Tile(Add("Gole extra na cabaça", "+1 gole por sala, até o fim da run.", 70, gold, p =>
            {
                p.Build.ExtraSips++;
                p.GainSip();
                return true;
            }), "tiles_house", 21, 13, 1, 1);
            EncounterText = "\"Mon na mão, mercadoria na mão.\" Segure E sobre o que quiser comprar.";
        }

        // ── Wandering smith ──────────────────────────────────────────────────────────

        void BuildSmith()
        {
            Color steel = ColorUtil.Hex("#C8C2B8");
            Tile(Add("Fundir", "Funde os dois itens mais fracos da mochila num item de raridade acima do melhor deles.", 30, steel, p =>
            {
                List<Item> bag = p.Build.Bag;
                if (bag.Count < 2)
                {
                    Say(p, "precisa de 2 itens na mochila", Config.palette.uiDim);
                    return false;
                }
                bag.Sort((x, y) => x.rarity.CompareTo(y.rarity));
                Item a = bag[0], c = bag[1];
                bag.RemoveRange(0, 2);
                var rarity = (Rarity)Mathf.Min(Rarities.Count - 1, Mathf.Max((int)a.rarity, (int)c.rarity) + 1);
                Item fused = ItemGenerator.RollAt(p.Build.Rng, rarity, null);
                bool eq = p.Build.AddItem(fused);
                p.ApplyStats(false);
                Fx.ItemCollected(fused, eq, p.Position);
                return true;
            }), "tiles_house", 19, 14, 1, 1);
            Tile(Add("Gravar", "Grava de novo o seu melhor item usado: mesmo espaço e raridade, atributos novos.", 25, steel, p =>
            {
                int best = -1;
                for (int i = 0; i < p.Build.Equipped.Length; i++)
                {
                    Item it = p.Build.Equipped[i];
                    if (it != null && (best < 0 || it.rarity > p.Build.Equipped[best].rarity)) best = i;
                }
                if (best < 0)
                {
                    Say(p, "nenhum item usado", Config.palette.uiDim);
                    return false;
                }
                Item old = p.Build.Equipped[best];
                p.Build.Equipped[best] = ItemGenerator.RollAt(p.Build.Rng, old.rarity, old.slot);
                p.Build.RecomputeModifiers();
                p.ApplyStats(false);
                Say(p, "GRAVADO: " + p.Build.Equipped[best].Name, Rarities.Color(old.rarity));
                return true;
            }), "tiles_house", 14, 11, 1, 2);
            Tile(Add("Afiar", "+6% de dano até o fim da run.", 40, steel, p =>
            {
                p.Build.Sharpened += 0.06f;
                p.Build.RecomputeModifiers();
                Say(p, "AFIADA", ColorUtil.Hex("#C8C2B8"));
                return true;
            }), "tiles_element", 6, 2, 1, 1);
            EncounterText = "O ferreiro errante bate o martelo sem olhar para você. Segure E para pedir um serviço.";
        }

        // ── Cursed treasure ──────────────────────────────────────────────────────────

        void BuildTreasure(SeededRandom rng, int depth)
        {
            Color purple = ColorUtil.Hex("#B06CFF");
            Tile(Add("Abrir o baú amaldiçoado", "Um item épico ou melhor… e uma maldição por 3 salas.", 0, purple, p =>
            {
                Rarity r = ItemGenerator.RollRarity(p.Build.Rng, depth + 6, true);
                if (r < Rarity.Epic) r = Rarity.Epic;
                Item item = ItemGenerator.RollAt(p.Build.Rng, r, null);
                bool eq = p.Build.AddItem(item);
                RunCurse curse = RunCurses.Random(p.Build.Rng);
                p.Build.ApplyCurse(curse, 3);
                p.ApplyStats(false);
                Fx.ItemCollected(item, eq, p.Position);
                Say(p, "MALDIÇÃO: " + RunCurses.Name(curse), purple);
                Fx.Emit(FxType.Curse, p.Position, Vector2.up, 1f, purple);
                return true;
            }), "tiles_element", 3, 1, 1, 1);
            EncounterText = "Um baú lacrado com selos rasgados. O que está dentro vale muito. O que vem junto, também.";
        }

        // ── The Gate of the Shuradō ──────────────────────────────────────────────────

        void BuildGate(SeededRandom rng)
        {
            gateOffer = KalpaRules.Offer(Run, rng);
            int next = Run.Kalpa + 1;
            EncounterTitle = "Portão do Shuradō · Kalpa " + KalpaRules.Numeral(next);
            EncounterText = "Para descer ao próximo ciclo, escolha a regra que o acompanhará para sempre." + (next >= 5 ? " A partir do Kalpa V, uma segunda regra vem junto." : "");
            foreach (KalpaRule rule in gateOffer)
            {
                KalpaRule r = rule;
                Tile(Add("Descer: " + KalpaRules.Name(r), KalpaRules.Text(r), 0, Config.palette.shu, p =>
                {
                    KalpaRules.Descend(Run, r, Run.Rng);
                    foreach (Offering o in Offerings)
                    {
                        if (!o.Exit) o.Spent = true;
                    }
                    Fx.Toast(string.Format(Strings.ToastKalpa, KalpaRules.Numeral(Run.Kalpa), KalpaRules.Name(r)), Config.palette.shu);
                    Fx.Emit(FxType.AsuraFall, p.Position, Vector2.up, 1f, Config.palette.shu);
                    return true;
                }, false), "tiles_dungeon", 6, 2, 1, 1);
            }
        }

        // ── Events (§6.7) ────────────────────────────────────────────────────────────

        void BuildEvent(int id)
        {
            EncounterTitle = RunEvents.Title(id);
            EncounterText = RunEvents.Text(id);
            Color c = ColorUtil.Hex("#6FC3DF");
            Color danger = Config.palette.danger;
            switch (id)
            {
                case 0: // The begging monk
                    Add("Dar 50 Mon", "Karma +1 e uma bênção.", 50, c, p =>
                    {
                        Run.Karma++;
                        GiveBlessing(p);
                        return true;
                    }).Character = "Monk";
                    break;
                case 1: // The kitsune in disguise
                    Add("Aceitar o chá", "Uma relíquia… 30% de ser maldita.", 0, c, p =>
                    {
                        GiveRelic(p);
                        if (p.Build.Rng.NextFloat() < 0.3f)
                        {
                            p.Build.ApplyCurse(RunCurse.NoHeal, 3);
                            Say(p, "O CHÁ ERA MALDITO", ColorUtil.Hex("#B06CFF"));
                        }
                        return true;
                    }).Character = "Shaman";
                    Tile(Add("Desmascarar", "Duelo com a raposa (o grupo todo). Vitória: relíquia e 40 Mon para cada um.", 0, danger, p =>
                    {
                        CloseChoices();
                        Enemy fox = SpawnEncounterFoe(EnemyDefs.Kitsune, true);
                        fox.OnDefeated = _ =>
                        {
                            foreach (PlayerController q in Players)
                            {
                                if (q.Build == null) continue;
                                GiveRelic(q);
                                q.Build.Mon += 40;
                            }
                        };
                        return true;
                    }, false), "tiles_house", 7, 15, 1, 2);
                    break;
                case 2: // Jizō
                    Tile(Add("Oferecer um item", "Entrega o item mais fraco da mochila; um item que você usa sobe de raridade.", 0, c, p =>
                    {
                        List<Item> bag = p.Build.Bag;
                        if (bag.Count == 0)
                        {
                            Say(p, "mochila vazia", Config.palette.uiDim);
                            return false;
                        }
                        bag.Sort((x, y) => x.rarity.CompareTo(y.rarity));
                        bag.RemoveAt(0);
                        var worn = new List<int>();
                        for (int i = 0; i < p.Build.Equipped.Length; i++)
                        {
                            if (p.Build.Equipped[i] != null && (int)p.Build.Equipped[i].rarity < Rarities.Count - 1) worn.Add(i);
                        }
                        if (worn.Count > 0)
                        {
                            int slot = worn[p.Build.Rng.Range(0, worn.Count)];
                            Item old = p.Build.Equipped[slot];
                            p.Build.Equipped[slot] = ItemGenerator.RollAt(p.Build.Rng, old.rarity + 1, old.slot);
                            p.Build.RecomputeModifiers();
                            p.ApplyStats(false);
                            Say(p, "JIZŌ ABENÇOOU: " + p.Build.Equipped[slot].Name, Rarities.Color(old.rarity + 1));
                        }
                        Run.Karma++;
                        return true;
                    }), "tiles_house", 5, 15, 1, 2);
                    Tile(Add("Rezar", "Cura 40% da vida.", 0, Config.palette.blessing, p =>
                    {
                        p.Heal(p.MaxHealth * 0.4f);
                        return true;
                    }), "tiles_house", 3, 17, 2, 2);
                    break;
                case 3: // The wounded ronin
                    Add("Ajudar", "Ele luta ao lado do grupo por 3 salas. Karma +1.", 0, c, p =>
                    {
                        CloseChoices();
                        Run.Karma++;
                        Run.AllyRooms = 3;
                        SpawnAlly();
                        return true;
                    }, false).Character = "SamuraiBlue";
                    Tile(Add("Finalizar", "A técnica dele é sua. Karma −2, Shura +15.", 0, danger, p =>
                    {
                        CloseChoices();
                        Run.Karma -= 2;
                        p.Build.AddPendingTechnique(RewardIds.PatientBlade);
                        p.AddShura(15f);
                        Fx.Emit(FxType.Finisher, p.Position + p.Facing, p.Facing, 1f, EnemyDefs.Ronin.bloodColor);
                        Say(p, "TÉCNICA ABSORVIDA", Config.palette.gold);
                        return true;
                    }, false), "tiles_house", 4, 4, 1, 1);
                    break;
                case 4: // Chō-han dice
                    Add("Apostar 40 Mon", "Par ou ímpar: dobra ou perde.", 40, c, p =>
                    {
                        if (p.Build.Rng.NextFloat() < 0.5f)
                        {
                            p.Build.Mon += 80;
                            Say(p, "CHŌ! +80 Mon", Config.palette.gold);
                        }
                        else Say(p, "HAN… perdeu", Config.palette.uiDim);
                        return true;
                    }).Character = "Hunter";
                    Tile(Add("Apostar 25 de vida", "Ganhe: 60 Mon. Perca: −25 de vida.", 0, danger, p =>
                    {
                        if (p.Build.Rng.NextFloat() < 0.5f)
                        {
                            p.Build.Mon += 60;
                            Say(p, "CHŌ! +60 Mon", Config.palette.gold);
                        }
                        else
                        {
                            p.ReceiveHazard(25f, 0f, Vector2.down);
                            Say(p, "HAN… −25", danger);
                        }
                        return true;
                    }), "tiles_element", 5, 0, 1, 1);
                    Tile(Add("Apostar uma relíquia", "Ganhe: outra relíquia e 30 Mon. Perca: uma das suas.", 0, danger, p =>
                    {
                        RewardDef owned = null;
                        foreach (RewardDef r in p.Build.Owned)
                        {
                            if (r.Kind == RewardKind.Relic) owned = r;
                        }
                        if (owned == null)
                        {
                            Say(p, "nenhuma relíquia", Config.palette.uiDim);
                            return false;
                        }
                        if (p.Build.Rng.NextFloat() < 0.5f)
                        {
                            GiveRelic(p);
                            p.Build.Mon += 30;
                        }
                        else
                        {
                            p.Build.Owned.Remove(owned);
                            p.Build.RecomputeModifiers();
                            p.ApplyStats(false);
                            Say(p, "PERDEU: " + owned.Name, danger);
                        }
                        return true;
                    }), "tiles_element", 4, 0, 1, 1);
                    break;
                case 5: // Onsen
                    Tile(Add("Banhar-se", "Cura total, limpa os ferimentos e enche a cabaça. Fluxo e Shura voltam a zero.", 0, c, p =>
                    {
                        p.Heal(p.MaxHealth);
                        p.ClearWounds();
                        p.EmptyMind();
                        p.RestoreBreath();
                        p.GainSip();
                        Say(p, "ÁGUA QUENTE", c);
                        return true;
                    }), "tiles_dungeon", 0, 2, 2, 1);
                    break;
                case 6: // The Muramasa blade
                    Tile(Add("Empunhar a Muramasa", "+35% de dano até o fim da run — e o Shura sobe sozinho. Karma −1.", 0, danger, p =>
                    {
                        p.Build.Muramasa = true;
                        p.Build.RecomputeModifiers();
                        Run.Karma--;
                        Say(p, "A LÂMINA TEM SEDE", danger);
                        Fx.Emit(FxType.ShuraRise, p.Position, Vector2.up, 2f, Config.palette.shu);
                        return true;
                    }), "tiles_house", 15, 11, 1, 2);
                    break;
                case 7: // The duel of honour
                    Add("Aceitar o duelo", "Sem técnicas, sem itens (o grupo todo). Vitória: Karma +2, 60 Mon e um item lendário para cada um.", 0, c, p =>
                    {
                        CloseChoices();
                        foreach (PlayerController q in Players) SetHonor(q, true);
                        Enemy foe = SpawnEncounterFoe(EnemyDefs.Kenkaku, true);
                        foe.OnDefeated = _ =>
                        {
                            Run.Karma += 2;
                            foreach (PlayerController q in Players)
                            {
                                SetHonor(q, false);
                                if (q.Build == null) continue;
                                q.Build.Mon += 60;
                                Item item = ItemGenerator.RollAt(q.Build.Rng, Rarity.Legendary, null);
                                bool eq = q.Build.AddItem(item);
                                q.ApplyStats(false);
                                Fx.ItemCollected(item, eq, q.Position);
                            }
                        };
                        return true;
                    }, false).Character = "SamuraiRed";
                    break;
            }
        }

        void CloseChoices()
        {
            foreach (Offering o in Offerings)
            {
                if (!o.Exit) o.Spent = true;
            }
        }

        static void SetHonor(PlayerController p, bool on)
        {
            if (p.Build == null) return;
            p.Build.HonorBound = on;
            p.Build.RecomputeModifiers();
            p.ApplyStats(false);
        }

        /// <summary>A fight inside an encounter: the torii stays shut until it is over.</summary>
        Enemy SpawnEncounterFoe(EnemyDef def, bool standoff)
        {
            Rect b = Layout.FloorBounds;
            Vector2 at = Layout.NearestFloor(new Vector2(b.center.x + 3f, b.center.y), def.radius);
            Enemy e = CreateEnemy(def, at, true, null);
            if (standoff) BeginStandoffWith(new List<Enemy> { e });
            return e;
        }

        void GiveRelic(PlayerController p)
        {
            var pool = new List<string>();
            foreach (string id in RewardIds.Relics)
            {
                if (!p.Build.Owns(id)) pool.Add(id);
            }
            if (pool.Count == 0)
            {
                GiveBlessing(p);
                return;
            }
            RewardDef r = RewardRegistry.Create(pool[p.Build.Rng.Range(0, pool.Count)]);
            if (r == null) return;
            p.Build.Acquire(r);
            p.ApplyStats(false);
            Say(p, string.Format(Strings.Obtained, r.Name), Config.palette.playerAccent);
        }

        void GiveBlessing(PlayerController p)
        {
            string[] all = RewardIds.Blessings;
            RewardDef r = RewardRegistry.Create(all[p.Build.Rng.Range(0, all.Length)]);
            if (r == null) return;
            p.Build.Acquire(r);
            p.ApplyStats(false);
            if (r.HealOnPick > 0f) p.Heal(r.HealOnPick);
            Say(p, string.Format(Strings.Obtained, r.Name), Config.palette.blessing);
        }
    }
}
