using System.Collections.Generic;
using UnityEngine;

namespace Shura
{
    /// <summary>What the evolved and fused techniques (design doc §4.4) need from the world.</summary>
    public sealed partial class World
    {
        /// <summary>Foes too important to be split or toyed with (Guardians, the duel, the dojo, the Inner Asura).</summary>
        public bool IsBossLike(Enemy e)
        {
            return e.IsElite || e.Def.superArmor || e.Training || e.AsuraOwner != null || TutorialRoom
                   || (CurrentRoom != null && CurrentRoom.kind == RoomKind.Duel);
        }

        /// <summary>A will-o'-wisp of the duelist's: a slow flame that burns whoever it touches.</summary>
        public void FireWisp(Vector2 origin, Vector2 direction, float damage, float posture)
        {
            Vector2 dir = direction.sqrMagnitude > 1e-4f ? direction.normalized : Vector2.right;
            Color c = EnemyDefs.Hitodama.color;
            Projectile p = CreateProjectile(origin + dir * 0.4f, dir, 9f, 0.32f, c, ShapeLibrary.Get(Shape.SoftCircle), 0.7f, false);
            p.Reflected = true;
            p.Damage = damage;
            p.Posture = posture;
            p.Life = 80;
            p.Element = Element.Fire;
            Fx.Emit(FxType.Spawn, origin, Vector2.up, 0.4f, c);
        }

        readonly List<Enemy> pendingDivides = new List<Enemy>();

        /// <summary>The enemy comes apart into weak minions (at the end of the tick, like every birth mid-fight).</summary>
        public void Divide(Enemy e, EnemyDef into, int count)
        {
            if (e == null || e.IsGone || pendingDivides.Contains(e)) return;
            pendingDivides.Add(e);
            int n = Mathf.Min(count, MaxEnemiesAlive - ActiveEnemyCount + 1);
            for (int i = 0; i < n; i++)
            {
                Vector2 at = e.Position + CombatMath.FromAngle(90f + i * 360f / Mathf.Max(1, n)) * 0.7f;
                pendingSpawns.Add(new PendingSpawn { def = into, at = Layout.NearestFloor(at, into.radius) });
            }
        }

        void FlushDivides()
        {
            if (pendingDivides.Count == 0) return;
            foreach (Enemy e in pendingDivides)
            {
                if (e.IsGone) continue;
                Fx.Emit(FxType.Burst, e.Position, Vector2.up, e.Radius * 2.4f, Elements.Color(Element.Wind));
                e.Dispel();
            }
            pendingDivides.Clear();
        }
    }
}
