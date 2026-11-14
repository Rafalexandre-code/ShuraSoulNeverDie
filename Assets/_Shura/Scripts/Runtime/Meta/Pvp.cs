using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>One duel in your record.</summary>
    [Serializable]
    public sealed class DuelResult
    {
        public string opponent = "";
        public string weapon = "";
        public string theirWeapon = "";
        /// <summary>1 won, 0 lost, 2 a draw.</summary>
        public int outcome;
        public int ratingAfter;
        /// <summary>What the duel moved the rating by.</summary>
        public int change;
        public string season = "";
    }

    /// <summary>Your PvP ladder for the current season (a calendar month), kept in your save.</summary>
    [Serializable]
    public sealed class PvpRecord
    {
        public string season = "";
        public int rating = Pvp.StartRating;
        public int wins;
        public int losses;
        public int draws;
        public int best = Pvp.StartRating;
        public List<DuelResult> history = new List<DuelResult>();
        /// <summary>Earlier seasons: "2026-10: 1084 (12V 7D)".</summary>
        public List<string> past = new List<string>();

        public void Sanitize()
        {
            history = history ?? new List<DuelResult>();
            past = past ?? new List<string>();
            history.RemoveAll(h => h == null);
            Presence.Trim(history, Pvp.HistoryKept);
            Presence.Trim(past, 12);
            if (rating <= 0) rating = Pvp.StartRating;
        }
    }

    /// <summary>
    /// Seasons and Elo for the duels (§9.6). With no server, each duelist keeps their own ladder:
    /// both sides see the same duel, so both compute the same change (each from their own side).
    /// </summary>
    public static class Pvp
    {
        public const int StartRating = 1000;
        public const int K = 32;
        public const int HistoryKept = 12;

        public static string SeasonOf(DateTime utc) => utc.ToString("yyyy-MM");

        public static string SeasonName(string season)
        {
            if (string.IsNullOrEmpty(season) || season.Length < 7) return season;
            string[] months = { "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro" };
            int m = int.TryParse(season.Substring(5, 2), out int v) ? v : 1;
            return months[Mathf.Clamp(m, 1, 12) - 1] + " de " + season.Substring(0, 4);
        }

        /// <summary>A new month opens a new season: the old one is written down, the ladder starts over.</summary>
        public static PvpRecord Current(MetaProgress meta, DateTime utc)
        {
            PvpRecord r = meta.pvp;
            string now = SeasonOf(utc);
            if (r.season != now)
            {
                if (!string.IsNullOrEmpty(r.season) && r.wins + r.losses + r.draws > 0)
                    r.past.Add(r.season + ": " + r.rating + " (" + r.wins + "V " + r.losses + "D)");
                r.season = now;
                r.rating = r.best = StartRating;
                r.wins = r.losses = r.draws = 0;
            }
            return r;
        }

        /// <summary>The Elo change for a result (1 won, 0.5 draw, 0 lost).</summary>
        public static int Change(int mine, int theirs, float score)
        {
            double expected = 1.0 / (1.0 + Math.Pow(10.0, (theirs - mine) / 400.0));
            return (int)Math.Round(K * (score - expected));
        }

        public static DuelResult Record(MetaProgress meta, DateTime utc, string opponent, int theirRating, int outcome, string weapon, string theirWeapon)
        {
            PvpRecord r = Current(meta, utc);
            float score = outcome == 1 ? 1f : outcome == 2 ? 0.5f : 0f;
            int before = r.rating;
            r.rating = Mathf.Max(100, r.rating + Change(r.rating, theirRating > 0 ? theirRating : StartRating, score));
            if (outcome == 1) r.wins++;
            else if (outcome == 2) r.draws++;
            else r.losses++;
            r.best = Mathf.Max(r.best, r.rating);
            var d = new DuelResult { opponent = opponent, outcome = outcome, ratingAfter = r.rating, change = r.rating - before, season = r.season, weapon = weapon, theirWeapon = theirWeapon };
            r.history.Add(d);
            Presence.Trim(r.history, HistoryKept);
            return d;
        }

        public static string RankTitle(int rating)
        {
            if (rating >= 1400) return "Kensei";
            if (rating >= 1250) return "Mestre";
            if (rating >= 1120) return "Veterano";
            if (rating >= 1000) return "Duelista";
            return "Aprendiz";
        }
    }
}
