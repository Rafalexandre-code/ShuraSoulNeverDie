using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>Challenge and dispute rooms (design doc §6.2).</summary>
    public sealed partial class World
    {
        /// <summary>The challenge's condition was broken this room.</summary>
        public bool ChallengeFailed { get; private set; }
        /// <summary>Seconds left in a timed challenge (0 when none).</summary>
        public float ChallengeTimeLeft { get; private set; }

        void ResetChallenge(RoomDef room)
        {
            ChallengeFailed = false;
            ChallengeTimeLeft = room.challenge == ChallengeKind.Timed ? RouteMap.ChallengeSeconds : 0f;
            if (room.challenge != ChallengeKind.None) HintOnce(HintId.Challenge);
        }

        void TickChallenge()
        {
            if (CurrentRoom == null || CurrentRoom.challenge != ChallengeKind.Timed || ChallengeFailed || RoomCleared) return;
            if (!roomActive) return;
            ChallengeTimeLeft -= SimClock.Dt;
            if (ChallengeTimeLeft <= 0f)
            {
                ChallengeTimeLeft = 0f;
                FailChallenge();
            }
        }

        /// <summary>The condition broke: the room goes on, the prize is gone.</summary>
        public void FailChallenge()
        {
            if (ChallengeFailed || CurrentRoom == null || CurrentRoom.challenge == ChallengeKind.None || RoomCleared) return;
            ChallengeFailed = true;
            Fx.Toast(Strings.ToastChallengeFailed, Config.palette.danger);
            Fx.Emit(FxType.FlowDown, CameraFocus.Position, Vector2.down);
        }

        /// <summary>"Só aparar": a dodge breaks the condition.</summary>
        public void OnDodgeForChallenge()
        {
            if (CurrentRoom != null && CurrentRoom.challenge == ChallengeKind.ParryOnly && ActiveEnemyCount > 0) FailChallenge();
        }

        /// <summary>Kept the condition to the end: everyone gets a rare item or better.</summary>
        void RewardChallenge()
        {
            if (CurrentRoom == null || CurrentRoom.challenge == ChallengeKind.None || ChallengeFailed) return;
            Fx.Toast(Strings.ToastChallengeDone, Config.palette.gold);
            foreach (PlayerController p in Players)
            {
                if (p.Build == null) continue;
                Rarity r = ItemGenerator.RollRarity(p.Build.Rng, Depth + 4, true);
                if (r < Rarity.Rare) r = Rarity.Rare;
                Item item = ItemGenerator.RollAt(p.Build.Rng, r, null);
                bool eq = p.Build.AddItem(item);
                p.ApplyStats(false);
                PlayerController before = actor;
                actor = p;
                Fx.ItemCollected(item, eq, p.Position);
                actor = before;
            }
        }

        /// <summary>Yokai and humans of the same wave go for each other first.</summary>
        void StartDispute(List<Enemy> wave)
        {
            var yokai = new List<Enemy>();
            var humans = new List<Enemy>();
            foreach (Enemy e in wave)
            {
                if (RoomGenerator.IsYokai(e.Def.id)) yokai.Add(e);
                else humans.Add(e);
            }
            if (yokai.Count == 0 || humans.Count == 0) return;
            for (int i = 0; i < yokai.Count; i++) yokai[i].StartBrawl(humans[i % humans.Count]);
            for (int i = 0; i < humans.Count; i++) humans[i].StartBrawl(yokai[i % yokai.Count]);
            HintOnce(HintId.Dispute);
        }
    }
}
