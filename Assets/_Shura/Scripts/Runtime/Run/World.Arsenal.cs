using UnityEngine;

namespace Shura
{
    /// <summary>The new arsenal's ways of fighting (design doc §4.2): what the world does for them.</summary>
    public sealed partial class World
    {
        const int KunaiReturnLife = 34;

        /// <summary>A kunai that flies out, cuts, and comes back to the hand.</summary>
        public void ThrowKunai(PlayerController thrower, Vector2 direction, float damage, float posture)
        {
            Sprite art = PixelArt.ProjectileSprite("fx_kunai");
            Vector2 dir = direction.sqrMagnitude > 1e-4f ? direction.normalized : thrower.Facing;
            Projectile p = CreateProjectile(thrower.Position + dir * (thrower.Radius + 0.3f), dir, 14f, 0.24f, Config.palette.playerAccent, art, 1f, false);
            p.Reflected = true;
            p.Piercing = true;
            p.Damage = damage;
            p.Posture = posture;
            p.Life = KunaiReturnLife * 2;
            p.Art = "fx_kunai";
            p.Thrower = thrower;
            Fx.Emit(FxType.EnemySwing, thrower.Position, dir, 2f);
        }

        /// <summary>The kunai turns home halfway; caught, it is spent. Returns true when it is gone.</summary>
        bool TickReturningKunai(Projectile p)
        {
            if (p.Thrower == null) return false;
            if (!p.Returning && p.Life <= KunaiReturnLife)
            {
                p.Returning = true;
                p.Struck.Clear();
            }
            if (!p.Returning) return false;
            Vector2 home = p.Thrower.Position - p.Position;
            if (home.magnitude < 0.6f || !p.Thrower.IsAlive) return true;
            p.Direction = home.normalized;
            p.Life = Mathf.Max(p.Life, 10);
            return false;
        }

        /// <summary>The fan's swing knocks every enemy projectile it touches back where it came from.</summary>
        public void DeflectProjectiles(Vector2 origin, Vector2 direction, HitShape shape)
        {
            for (int i = 0; i < Projectiles.Count; i++)
            {
                Projectile p = Projectiles[i];
                if (p.Reflected || !CombatMath.Overlaps(shape, origin, direction, p.Position, p.Radius + 0.3f)) continue;
                Reflect(p);
                Fx.Emit(FxType.PerfectParry, p.Position, -p.Direction, 1f, Elements.Color(Element.Wind));
            }
        }

        /// <summary>A strike of the player's weapon met an enemy: the traits that change the numbers.</summary>
        void ApplyWeaponTraits(Enemy e, Vector2 hitDir, ref float damage)
        {
            WeaponDef w = Player.Weapon;
            if (w == null) return;
            if (w.spiritMult != 1f && RoomGenerator.IsYokai(e.Def.id)) damage *= w.spiritMult;
            if (w.backstabMult != 1f && Vector2.Dot(e.Facing, hitDir) > 0.35f)
            {
                damage *= w.backstabMult;
                Fx.Popup(Strings.PopupBackstab, e.Position + Vector2.up * 1.1f, Elements.Color(Element.Shadow));
            }
        }

        /// <summary>The Shakujō's rings: spirits in reach turn solid before the blow lands.</summary>
        void ExposeInReach(Vector2 origin, Vector2 direction, HitShape shape)
        {
            if (Player.Weapon == null || !Player.Weapon.exposes) return;
            for (int i = 0; i < Enemies.Count; i++)
            {
                Enemy e = Enemies[i];
                if (e.IsGone || e.IsAlly || !e.Def.ethereal) continue;
                if (CombatMath.Overlaps(shape, origin, direction, e.Position, e.Radius)) e.ForceExpose(150);
            }
        }

        void AfterWeaponHit(Enemy e)
        {
            WeaponDef w = Player.Weapon;
            if (w == null || w.stunOnHit <= 0 || e.IsGone) return;
            e.Stun(e.IsElite || IsBossLike(e) ? w.stunOnHit / 3 : w.stunOnHit);
        }

        /// <summary>The swap strike's own power, as it begins.</summary>
        public void WeaponSwapPower(PlayerController p, WeaponPower power)
        {
            Palette pal = Config.palette;
            switch (power)
            {
                case WeaponPower.FoldedWind:
                    // Every hostile projectile in the room turns around; allies breathe.
                    for (int i = 0; i < Projectiles.Count; i++)
                    {
                        if (!Projectiles[i].Reflected) Reflect(Projectiles[i]);
                    }
                    foreach (PlayerController ally in Players)
                    {
                        if (!ally.IsAlive) continue;
                        ally.Posture.Reset(0f);
                        if (ally != p) Fx.Emit(FxType.Heal, ally.Position, Vector2.up, ally.Radius, Elements.Color(Element.Wind));
                    }
                    Fx.Emit(FxType.Burst, p.Position, p.Facing, 4f, Elements.Color(Element.Wind));
                    break;
                case WeaponPower.Earthquake:
                    foreach (Enemy e in Enemies)
                    {
                        if (!e.CanBeHit || e.IsAlly || Vector2.Distance(e.Position, p.Position) > 3.6f) continue;
                        e.Stun(IsBossLike(e) ? 30 : 90);
                    }
                    Fx.Emit(FxType.SlamImpact, p.Position, p.Facing, 3.6f, ColorUtil.Hex("#8C6E54"));
                    break;
                case WeaponPower.Toll:
                    foreach (Enemy e in Enemies)
                    {
                        if (e.IsGone || e.IsAlly || Vector2.Distance(e.Position, p.Position) > 4.5f) continue;
                        if (e.Def.ethereal) e.ForceExpose(300);
                        if (RoomGenerator.IsYokai(e.Def.id)) e.Stun(IsBossLike(e) ? 20 : 60);
                    }
                    Fx.Emit(FxType.Elo, p.Position, Vector2.up, 3f, pal.gold);
                    Fx.Emit(FxType.Burst, p.Position, Vector2.up, 4.5f, pal.gold);
                    break;
                case WeaponPower.DoubleShadow:
                {
                    Enemy target = FindNearestEnemy(p.Position, null);
                    if (target == null || Vector2.Distance(target.Position, p.Position) > 7f) break;
                    Vector2 behind = Layout.NearestFloor(target.Position - target.Facing * (target.Radius + 0.9f), p.Radius);
                    Fx.Emit(FxType.Smoke, p.Position, Vector2.up, p.Radius, Elements.Color(Element.Shadow));
                    Fx.Emit(FxType.Afterimage, p.Position, p.Facing, p.Radius * 1.3f, Elements.Color(Element.Shadow));
                    p.Teleport(behind);
                    Vector2 to = target.Position - behind;
                    if (to.sqrMagnitude > 1e-4f) p.Facing = to.normalized;
                    Fx.Emit(FxType.Smoke, behind, Vector2.up, p.Radius, Elements.Color(Element.Shadow));
                    break;
                }
            }
        }

        /// <summary>The finisher's own power, once the enemy is cut down.</summary>
        void WeaponFinisherPower(Enemy enemy, Vector2 dir)
        {
            WeaponDef w = Player.Weapon;
            if (w == null) return;
            switch (w.finisherPower)
            {
                case WeaponPower.CloseFan:
                    FireSlashWave(enemy.Position, dir, 14f, 18f);
                    break;
                case WeaponPower.CrushOni:
                    ScheduleBurst(enemy.Position, 4, 2.6f, 16f, 30f, ColorUtil.Hex("#8C6E54"), false, 60);
                    Fx.Emit(FxType.SlamImpact, enemy.Position, dir, 2.6f, ColorUtil.Hex("#8C6E54"));
                    break;
                case WeaponPower.Exorcism:
                    foreach (Enemy e in Enemies)
                    {
                        if (e == enemy || e.IsGone || e.IsAlly || !RoomGenerator.IsYokai(e.Def.id)) continue;
                        if (Vector2.Distance(e.Position, enemy.Position) > 4f) continue;
                        if (e.Def.minion && !IsBossLike(e)) e.Dispel();
                        else e.ReceivePlayerHit(30f, 30f, (e.Position - enemy.Position).normalized, true);
                        Fx.Emit(FxType.Burst, e.Position, Vector2.up, e.Radius * 2.5f, Config.palette.gold);
                    }
                    Fx.Popup(Strings.PopupExorcism, enemy.Position + Vector2.up * 1.6f, Config.palette.gold);
                    break;
                case WeaponPower.HiddenTruth:
                    foreach (Enemy e in Enemies)
                    {
                        if (e.CanBeHit && !e.IsAlly && Vector2.Distance(e.Position, Player.Position) <= 6f) e.Confuse(120);
                    }
                    Player.GrantInvulnerable(45);
                    Fx.Emit(FxType.Smoke, Player.Position, Vector2.up, Player.Radius * 2f, Elements.Color(Element.Shadow));
                    break;
            }
        }
    }
}
