using UnityEngine;

namespace Shura
{
    /// <summary>What one journey left behind, for the end screen.</summary>
    public sealed class LegacyReport
    {
        public int steel;
        public int fragments;
        public int petals;
        public int karma;
        public int heat;
        /// <summary>The disciple fell before depth 10: a tomb and an heir.</summary>
        public bool died;
        /// <summary>Came home past 70: a Sensei now, and an heir.</summary>
        public bool retired;
        /// <summary>Came home past depth 10: five years older.</summary>
        public bool aged;
        public Disciple disciple;
        public string jisei = "";
        /// <summary>Systems that opened with this run (§7.7), to announce.</summary>
        public string unlocked = "";
        /// <summary>The road ended in victory (Tower, Pilgrimage): its ending.</summary>
        public string ending = "";
        /// <summary>Who killed you and remembers it now (§5.1), and the Ecos it took.</summary>
        public string rival = "";
        public int rivalEcos;
        public bool rivalPromoted;
    }

    /// <summary>
    /// Banks the Phase 4 side of a run: the persistent currencies, the Karma, and the lineage
    /// (age, death, retirement, the tomb's poem and the three heirs).
    /// </summary>
    public static class Legacy
    {
        /// <summary>Shura (−) and Bushidō (+) gained on the road weigh 5 each on the persistent scale.</summary>
        public const int KarmaWeight = 5;

        /// <param name="died">The run ended in death (quitting or restarting does not kill the disciple).</param>
        public static LegacyReport Bank(MetaProgress meta, RunState run, Lineage.Facts facts, bool died, bool lineage = true)
        {
            var r = new LegacyReport { heat = run.Oaths.Heat };
            PlayerBuild b = run.Main;
            r.steel = Temple.SteelForRun(b.CelestialSteel, run.ElitesKilled);
            r.fragments = b.SoulFragments;
            r.petals = Oaths.Petals(r.heat, run.RoomsCleared, run.ChallengesWon);
            r.karma = run.Karma * KarmaWeight + (b.Muramasa ? -10 : 0) + (b.Shura >= 100f ? -5 : 0);
            meta.steel += r.steel;
            meta.fragments += r.fragments;
            meta.petals += r.petals;
            meta.AddKarma(r.karma);
            meta.bestHeat = Mathf.Max(meta.bestHeat, r.heat);
            meta.AddStat(TeaGuests.StatSteel, r.steel);
            meta.AddStat(TeaGuests.StatParries, run.PerfectParries);

            if (meta.disciple == null) meta.disciple = Lineage.First();
            Disciple d = meta.disciple;
            r.disciple = d;
            // Before the lineage opens, the first body neither ages nor dies.
            if (!lineage) return r;
            r.disciple = d;
            d.bestRoom = Mathf.Max(d.bestRoom, run.RoomsCleared);
            d.weapon = b.CurrentWeapon != null ? b.CurrentWeapon.id : "";
            if (run.RoomsCleared >= Lineage.SurviveDepth)
            {
                d.journeys++;
                d.age += Lineage.YearsPerJourney;
                r.aged = true;
                if (d.age >= Lineage.RetireAge)
                {
                    d.retired = true;
                    r.retired = true;
                    Bury(meta, d, run);
                }
            }
            else if (died)
            {
                r.died = true;
                d.cause = facts.killer ?? "";
                d.karma = run.Karma;
                d.jisei = Lineage.Jisei(facts);
                r.jisei = d.jisei;
                Bury(meta, d, run);
            }
            return r;
        }

        /// <summary>To the graveyard; three heirs wait, the next generation inheriting a technique.</summary>
        static void Bury(MetaProgress meta, Disciple d, RunState run)
        {
            meta.cemetery.Add(d);
            meta.disciple = null;
            string technique = MostUsedTechnique(run.Main);
            if (string.IsNullOrEmpty(technique)) technique = d.technique;
            meta.heirs = Lineage.Candidates(run.Seed ^ (ulong)meta.runs, d.generation + 1, technique);
        }

        /// <summary>The technique raised the highest (the first absorbed among equals).</summary>
        public static string MostUsedTechnique(PlayerBuild b)
        {
            TechniqueDef best = null;
            foreach (RewardDef r in b.Owned)
            {
                if (r is TechniqueDef t && (best == null || t.Level > best.Level)) best = t;
            }
            return best?.Id;
        }

        /// <summary>The heir chosen at the graveyard becomes the living disciple.</summary>
        public static bool ChooseHeir(MetaProgress meta, int index)
        {
            if (meta.heirs == null || index < 0 || index >= meta.heirs.Count) return false;
            meta.disciple = meta.heirs[index];
            meta.heirs.Clear();
            return true;
        }
    }
}
