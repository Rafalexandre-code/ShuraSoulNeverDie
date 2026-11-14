using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>One enemy's scroll: five challenges, kept across every life (design doc §7.6).</summary>
    [Serializable]
    public sealed class EnemyScroll
    {
        public string id;
        /// <summary>Its signature blow, perfectly parried.</summary>
        public int signatureParries;
        /// <summary>The weapons (base ids) that finished it.</summary>
        public List<string> weapons = new List<string>();
        /// <summary>Defeated without it ever touching a duelist.</summary>
        public int cleanKills;
        /// <summary>Its signature answered with the master's answer.</summary>
        public int masterAnswers;
        /// <summary>Defeated as a rival (vengeance).</summary>
        public int rivalKills;
        /// <summary>All the open challenges done: the painting, the legend, the technique's variant.</summary>
        public bool done;

        public void Sanitize()
        {
            signatureParries = Mathf.Max(0, signatureParries);
            cleanKills = Mathf.Max(0, cleanKills);
            masterAnswers = Mathf.Max(0, masterAnswers);
            rivalKills = Mathf.Max(0, rivalKills);
            if (weapons == null) weapons = new List<string>();
            weapons.RemoveAll(string.IsNullOrEmpty);
        }
    }

    public enum ScrollEvent
    {
        SignatureParry,
        Finisher,
        CleanKill,
        MasterAnswer,
        RivalDefeat,
    }

    /// <summary>
    /// The five challenges: parry the signature 30 times, finish it with three different
    /// weapons, win without taking its damage, use the master's answer, and defeat it as a
    /// rival (sealed until the Rivals). Completing the four open ones paints the enemy in
    /// sumi-e, writes its legend, gives Soul Fragments, and its technique is absorbed already
    /// at level II.
    /// </summary>
    public static class EnemyScrolls
    {
        public const int SignatureGoal = 30;
        public const int WeaponGoal = 3;
        public const int FragmentReward = 3;
        public const int Challenges = 5;

        public static string ChallengeName(int i)
        {
            switch (i)
            {
                case 0: return "Aparar a assinatura " + SignatureGoal + " vezes";
                case 1: return "Finalizar com " + WeaponGoal + " armas diferentes";
                case 2: return "Vencer sem levar dano dele";
                case 3: return "Usar a resposta de mestre";
                default: return "Derrotá-lo como rival";
            }
        }

        /// <summary>Progress of one challenge, 0..1 (the rival's stays at 0 for now).</summary>
        public static float ChallengeProgress(EnemyScroll s, int i)
        {
            if (s == null) return 0f;
            switch (i)
            {
                case 0: return Mathf.Clamp01(s.signatureParries / (float)SignatureGoal);
                case 1: return Mathf.Clamp01(s.weapons.Count / (float)WeaponGoal);
                case 2: return s.cleanKills > 0 ? 1f : 0f;
                case 3: return s.masterAnswers > 0 ? 1f : 0f;
                default: return s.rivalKills > 0 ? 1f : 0f;
            }
        }

        public static string ChallengeCount(EnemyScroll s, int i)
        {
            switch (i)
            {
                case 0: return Mathf.Min(s?.signatureParries ?? 0, SignatureGoal) + "/" + SignatureGoal;
                case 1: return Mathf.Min(s?.weapons.Count ?? 0, WeaponGoal) + "/" + WeaponGoal;
                case 2: return (s != null && s.cleanKills > 0 ? 1 : 0) + "/1";
                case 3: return (s != null && s.masterAnswers > 0 ? 1 : 0) + "/1";
                default: return (s != null && s.rivalKills > 0 ? 1 : 0) + "/1";
            }
        }

        static bool OpenOnesDone(EnemyScroll s)
        {
            for (int i = 0; i < Challenges; i++)
            {
                if (ChallengeProgress(s, i) < 1f) return false;
            }
            return true;
        }

        /// <summary>Records one deed; returns true when it just completed the scroll.</summary>
        public static bool Record(MetaProgress meta, string enemyId, ScrollEvent e, string weapon = null)
        {
            if (meta == null || string.IsNullOrEmpty(enemyId)) return false;
            EnemyScroll s = meta.Scroll(enemyId, true);
            switch (e)
            {
                case ScrollEvent.SignatureParry: s.signatureParries++; break;
                case ScrollEvent.CleanKill: s.cleanKills++; break;
                case ScrollEvent.MasterAnswer: s.masterAnswers++; break;
                case ScrollEvent.RivalDefeat: s.rivalKills++; break;
                case ScrollEvent.Finisher:
                    if (!string.IsNullOrEmpty(weapon) && !s.weapons.Contains(weapon)) s.weapons.Add(weapon);
                    break;
            }
            if (s.done || !OpenOnesDone(s)) return false;
            s.done = true;
            meta.fragments += FragmentReward;
            return true;
        }

        public static bool Done(MetaProgress meta, string enemyId) => meta?.Scroll(enemyId, false)?.done ?? false;

        /// <summary>The enemy that leaves this technique (null if none).</summary>
        public static EnemyDef SourceOf(string techniqueId)
        {
            if (string.IsNullOrEmpty(techniqueId)) return null;
            foreach (EnemyDef e in EnemyDefs.All)
            {
                if (e.Teaches == techniqueId) return e;
            }
            return null;
        }

        static readonly string[] Origins =
        {
            "um soldado que nunca voltou da guerra", "um guarda de ponte que dormiu no posto", "um herdeiro que matou o irmão pelo nome da casa",
            "um caçador que perdeu a família para a fome", "um monge que quebrou o voto por raiva", "um mercenário pago duas vezes pela mesma morte",
            "um pescador que jurou vingança ao mar", "um guarda-costas que fugiu quando o senhor mais precisou",
        };

        static readonly string[] Falls =
        {
            "morreu com a lâmina ainda erguida", "caiu sem saber o nome de quem o derrubou", "pediu mais uma luta ao último suspiro",
            "morreu sorrindo, e o sorriso ficou", "jurou que voltaria, e a roda ouviu", "morreu de frio esperando um inimigo que não veio",
        };

        /// <summary>How it came to the Shuradō (§8.7): the same story every time for the same enemy.</summary>
        public static string Legend(EnemyDef e)
        {
            int h = 17;
            foreach (char c in e.id) h = h * 31 + c;
            h &= 0x7FFFFFFF;
            return "Antes de ser " + e.displayName + ", foi " + Origins[h % Origins.Length] + ". " +
                   char.ToUpperInvariant(Falls[(h / 7) % Falls.Length][0]) + Falls[(h / 7) % Falls.Length].Substring(1) +
                   ", e o Shuradō o reclamou. Agora luta para sempre, e lembra de cada um que o venceu.";
        }
    }
}
