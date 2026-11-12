using UnityEngine;

namespace Shura
{
    /// <summary>
    /// A rival of the Shuradō (design doc §5.1), and the enemies who lose heart (§5.7): after
    /// their leader falls to a finisher at full Flow, humans may kneel or run. A kneeling enemy
    /// waits for the duelist's choice: Interact spares it (+Bushidō), a blow executes it (+Shura).
    /// </summary>
    public sealed partial class Enemy
    {
        /// <summary>The rival's id in the soul's memory (null for ordinary enemies).</summary>
        public string RivalId { get; private set; }
        public string RivalName { get; private set; }
        public bool IsRival => RivalId != null;

        public bool Kneeling { get; private set; }
        public bool Fleeing { get; private set; }
        /// <summary>No longer fighting: does not count for the room, never attacks.</summary>
        public bool Yielding => Kneeling || Fleeing;
        Vector2 fleeTo;
        int yieldFrames;

        public void MakeRival(Rival r)
        {
            RivalId = r.id;
            RivalName = r.name;
        }

        public void Kneel()
        {
            if (IsGone) return;
            world.Coordinator.Release(this);
            CurrentMove = null;
            Kneeling = true;
            Velocity = Vector2.zero;
            Enter(State.Engage);
            Squash(1.25f, 0.7f);
            world.Fx.Popup(Strings.PopupKneels, Position + Vector2.up * 1.2f, Color.white);
        }

        public void Flee(Vector2 to)
        {
            if (IsGone) return;
            world.Coordinator.Release(this);
            CurrentMove = null;
            Fleeing = true;
            fleeTo = to;
            Enter(State.Engage);
            world.Fx.Popup(Strings.PopupFlees, Position + Vector2.up * 1.2f, pal.uiDim);
        }

        /// <summary>Spared: it bows, and walks out of the Shuradō for this life.</summary>
        public void Spare()
        {
            Kneeling = false;
            Fleeing = true;
            fleeTo = Position + (Position - world.Layout.FloorBounds.center).normalized * 30f;
        }

        /// <summary>Kneeling or running instead of fighting (returns true when it handled the tick).</summary>
        bool TickYield()
        {
            if (!Yielding) return false;
            yieldFrames++;
            if (Kneeling)
            {
                Velocity = Vector2.Lerp(Velocity, Vector2.zero, 0.3f);
                if (yieldFrames % 40 == 0) Squash(1.12f, 0.85f);
                Integrate();
                return true;
            }
            Vector2 d = fleeTo - Position;
            Velocity = d.sqrMagnitude > 0.01f ? world.Layout.Steer(Position, fleeTo, Radius) * Def.moveSpeed * 1.3f : Vector2.zero;
            if (Velocity.sqrMagnitude > 0.01f) Facing = Velocity.normalized;
            Integrate();
            // Out of sight (or long enough): gone without a body.
            if (yieldFrames > 150 || !world.Layout.IsFloorAt(Position + Velocity.normalized * (Radius + 0.4f)) || d.magnitude < 0.6f) Dispel();
            return true;
        }
    }
}
