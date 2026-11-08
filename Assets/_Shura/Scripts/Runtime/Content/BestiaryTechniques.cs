using UnityEngine;

namespace Shura
{
    // Techniques absorbed from the bestiary (M1.10). Each one turns the enemy's own mechanic
    // into a tool for the player, and every one has a price. Like the original eight, an
    // instance holds per-run state; in co-op the whole party shares the run's techniques.

    /// <summary>Kunoichi: perfect parries answer with a fan of kunai.</summary>
    public sealed class KunaiFanTechnique : RewardDef
    {
        public override string Id => RewardIds.KunaiFan;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Leque de Kunai";
        public override string Gain => "Parry perfeito lança 3 kunai em leque na direção de quem atacou (6 dano, 8 postura cada).";
        public override string Cost => "Seu parry perfeito causa 15% menos postura.";
        public override string SourceEnemy => EnemyDefs.Kunoichi.displayName;

        public override void Apply(PlayerModifiers m) { m.perfectParryPostureMult *= 0.85f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            Vector2 dir = attacker != null ? attacker.Position - world.Player.Position : world.Player.Facing;
            if (dir.sqrMagnitude < 1e-4f) dir = world.Player.Facing;
            world.FirePlayerKunai(world.Player.Position, dir.normalized, 3, 30f, 6f, 8f);
        }
    }

    /// <summary>Bombardeiro: a dodge leaves a bomb where you stood.</summary>
    public sealed class ShortFuseTechnique : RewardDef
    {
        const float Cooldown = 3f;
        float last = -99f;

        public override string Id => RewardIds.ShortFuse;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Pavio Curto";
        public override string Gain => "Esquivar deixa uma bomba onde você estava (explode em 0,7 s: 12 dano, 18 postura). Uma a cada 3 s.";
        public override string Cost => "-8 de vida máxima.";
        public override string SourceEnemy => EnemyDefs.Bombardeiro.displayName;

        public override void Apply(PlayerModifiers m) { m.maxHealthBonus -= 8f; }

        public override void OnDodge(World world, Vector2 from)
        {
            if (world.Run.Elapsed - last < Cooldown) return;
            last = world.Run.Elapsed;
            world.ScheduleBurst(from, 42, 1.6f, 12f, 18f, World.BlastColor(BlastKind.Bomb), true);
        }
    }

    /// <summary>Tengu: a blade kept sheathed for a moment comes out as a crushing draw.</summary>
    public sealed class IaiDrawTechnique : RewardDef
    {
        const float ChargeSeconds = 1f;
        float lastSwing = -99f;
        bool charged;
        int combo;

        public override string Id => RewardIds.IaiDraw;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Saque Iai";
        public override string Gain => "Ficar 1 s sem atacar carrega o saque: o próximo golpe causa +80% de dano e 2x postura.";
        public override string Cost => "O 2º e o 3º golpes do combo causam 15% menos dano.";
        public override string SourceEnemy => EnemyDefs.Tengu.displayName;

        public override void OnSwing(World world, int comboIndex, Vector2 direction)
        {
            charged = world.Run.Elapsed - lastSwing >= ChargeSeconds;
            lastSwing = world.Run.Elapsed;
            combo = comboIndex;
            if (charged) world.Fx.Popup("IAI", world.Player.Position + Vector2.up * 1f, world.Config.palette.gold);
        }

        public override void ModifyAttackDamage(World world, Enemy target, ref float damage)
        {
            if (charged) damage *= 1.8f;
            else if (combo > 0) damage *= 0.85f;
        }

        public override void ModifyAttackPosture(World world, Enemy target, ref float posture)
        {
            if (charged) posture *= 2f;
        }
    }

    /// <summary>Onmyōji: a perfect parry seals the floor under the attacker.</summary>
    public sealed class ExplosiveSealTechnique : RewardDef
    {
        public override string Id => RewardIds.ExplosiveSeal;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Selo Explosivo";
        public override string Gain => "Parry perfeito marca o chão sob quem atacou: explode em 0,8 s (14 dano, 22 postura em todos ao redor).";
        public override string Cost => "Seu parry perfeito causa 20% menos postura direta.";
        public override string SourceEnemy => EnemyDefs.Onmyoji.displayName;

        public override void Apply(PlayerModifiers m) { m.perfectParryPostureMult *= 0.8f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            if (attacker == null) return;
            world.ScheduleBurst(attacker.Position, 48, 1.6f, 14f, 22f, World.BlastColor(BlastKind.Talisman), true);
        }
    }

    /// <summary>Xamã: every finisher mends the whole party.</summary>
    public sealed class ShamanBlessingTechnique : RewardDef
    {
        const float Heal = 10f;

        public override string Id => RewardIds.ShamanBlessing;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Bênção da Xamã";
        public override string Gain => "Toda finalização cura 10 de vida de TODO o grupo e zera a sua postura.";
        public override string Cost => "-10% de dano.";
        public override string SourceEnemy => EnemyDefs.Curandeira.displayName;

        public override void Apply(PlayerModifiers m) { m.damageMult *= 0.9f; }

        public override void OnFinisher(World world, Enemy target)
        {
            foreach (PlayerController p in world.Players)
            {
                if (!p.IsAlive) continue;
                p.Heal(Heal);
                world.Fx.Emit(FxType.Heal, p.Position, Vector2.up, p.Radius, EnemyDefs.Curandeira.color);
            }
            world.Player.Posture.Reset(0f);
        }
    }

    /// <summary>Chōchin-obake: what you kill explodes.</summary>
    public sealed class LivingLanternTechnique : RewardDef
    {
        public override string Id => RewardIds.LivingLantern;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Lanterna Viva";
        public override string Gain => "Todo inimigo que cai explode após 0,4 s (raio 1,8: 12 dano, 18 postura nos outros inimigos).";
        public override string Cost => "Você recebe 10% mais dano.";
        public override string SourceEnemy => EnemyDefs.Lanterna.displayName;

        public override void Apply(PlayerModifiers m) { m.damageTakenMult *= 1.1f; }

        public override void OnKill(World world, Enemy enemy)
        {
            world.ScheduleBurst(enemy.Position, 24, 1.8f, 12f, 18f, World.BlastColor(BlastKind.Fire), true);
        }
    }

    /// <summary>Onryō: a perfect parry makes you untouchable for a heartbeat.</summary>
    public sealed class EtherealBodyTechnique : RewardDef
    {
        public override string Id => RewardIds.EtherealBody;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Corpo Etéreo";
        public override string Gain => "Após um parry perfeito, nada te acerta por 0,5 s.";
        public override string Cost => "Suas esquivas recarregam 25% mais devagar.";
        public override string SourceEnemy => EnemyDefs.Onryo.displayName;

        public override void Apply(PlayerModifiers m) { m.dodgeRechargeMult *= 1.25f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            world.Player.GrantInvulnerable(30);
            world.Fx.Emit(FxType.Phase, world.Player.Position, Vector2.up, world.Player.Radius, EnemyDefs.Onryo.color);
        }
    }

    /// <summary>Kappa: a perfect dodge gives the charge back, and a sip of health.</summary>
    public sealed class KappaCurrentTechnique : RewardDef
    {
        public override string Id => RewardIds.KappaCurrent;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Correnteza do Kappa";
        public override string Gain => "Esquiva perfeita devolve a carga de esquiva e cura 4.";
        public override string Cost => "Suas esquivas são 15% mais curtas.";
        public override string SourceEnemy => EnemyDefs.Kappa.displayName;

        public override void Apply(PlayerModifiers m) { m.dodgeDistanceMult *= 0.85f; }

        public override void OnPerfectDodge(World world, Enemy attacker)
        {
            world.Player.RefundDodge();
            world.Player.Heal(4f);
        }
    }

    /// <summary>Oni Ciclope: enemies pinned against a wall or statue break twice as fast.</summary>
    public sealed class OniCrushTechnique : RewardDef
    {
        public override string Id => RewardIds.OniCrush;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Golpe do Oni";
        public override string Gain => "Golpes em inimigos encostados numa parede ou estátua causam 2x postura.";
        public override string Cost => "Você anda 8% mais devagar.";
        public override string SourceEnemy => EnemyDefs.Ciclope.displayName;

        public override void Apply(PlayerModifiers m) { m.moveSpeedMult *= 0.92f; }

        public override void ModifyAttackPosture(World world, Enemy target, ref float posture)
        {
            if (world.Layout == null || world.Layout.Fits(target.Position, target.Radius + 0.45f)) return;
            posture *= 2f;
            world.Fx.Popup("CONTRA A PAREDE", target.Position + Vector2.up * 1f, world.Config.palette.gold);
        }
    }

    /// <summary>Lodo: every fallen enemy feeds you.</summary>
    public sealed class SlimeHungerTechnique : RewardDef
    {
        const float Heal = 5f;

        public override string Id => RewardIds.SlimeHunger;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Fome do Lodo";
        public override string Gain => "Cada inimigo que cai cura 5 de vida (lacaios não contam).";
        public override string Cost => "-10 de vida máxima.";
        public override string SourceEnemy => EnemyDefs.Lodo.displayName;

        public override void Apply(PlayerModifiers m) { m.maxHealthBonus -= 10f; }

        public override void OnKill(World world, Enemy enemy)
        {
            if (enemy.Def.minion || !world.Player.IsAlive) return;
            world.Player.Heal(Heal);
            world.Fx.Popup("+" + Mathf.RoundToInt(Heal), world.Player.Position + Vector2.up * 0.8f, world.Config.palette.health);
        }
    }

    /// <summary>Bárbaro: the closer to death, the harder you hit.</summary>
    public sealed class FrenzyTechnique : RewardDef
    {
        public override string Id => RewardIds.Frenzy;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Fúria do Bárbaro";
        public override string Gain => "Abaixo de 50% de vida, seus golpes causam +40% de dano.";
        public override string Cost => "Acima de 50% de vida, seus golpes causam 10% menos dano.";
        public override string SourceEnemy => EnemyDefs.Berserker.displayName;

        public override void ModifyAttackDamage(World world, Enemy target, ref float damage)
        {
            PlayerController p = world.Player;
            damage *= p.Health < p.MaxHealth * 0.5f ? 1.4f : 0.9f;
        }
    }

    /// <summary>Ninja do Trovão: the last blow of every combo is repeated by a shadow.</summary>
    public sealed class ShadowCopyTechnique : RewardDef
    {
        public override string Id => RewardIds.ShadowCopy;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Cópia de Sombra";
        public override string Gain => "O 3º golpe do combo é repetido por uma cópia 0,25 s depois (8 dano, 10 postura).";
        public override string Cost => "-8 de vida máxima.";
        public override string SourceEnemy => EnemyDefs.Raijin.displayName;

        public override void Apply(PlayerModifiers m) { m.maxHealthBonus -= 8f; }

        public override void OnSwing(World world, int comboIndex, Vector2 direction)
        {
            if (comboIndex < 2) return;
            world.ScheduleBurst(world.Player.Position + direction * 1.2f, 15, 1.3f, 8f, 10f, EnemyDefs.Raijin.color);
        }
    }

    /// <summary>Olho Vigia: a perfect dodge blinks you behind the attacker.</summary>
    public sealed class SentinelBlinkTechnique : RewardDef
    {
        public override string Id => RewardIds.SentinelBlink;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Piscar do Vigia";
        public override string Gain => "Esquiva perfeita te teleporta para as costas de quem atacou, já virado para ele.";
        public override string Cost => "Suas esquivas recarregam 20% mais devagar.";
        public override string SourceEnemy => EnemyDefs.Olho.displayName;

        public override void Apply(PlayerModifiers m) { m.dodgeRechargeMult *= 1.2f; }

        public override void OnPerfectDodge(World world, Enemy attacker)
        {
            if (attacker == null || attacker.IsGone) return;
            PlayerController p = world.Player;
            Vector2 behind = world.Layout.NearestFloor(attacker.Position - attacker.Facing * (attacker.Radius + 1.1f), p.Radius);
            world.Fx.Emit(FxType.Smoke, p.Position, Vector2.up, p.Radius, Color.white);
            p.Teleport(behind);
            Vector2 to = attacker.Position - behind;
            if (to.sqrMagnitude > 1e-4f) p.Facing = to.normalized;
            world.Fx.Emit(FxType.Smoke, behind, Vector2.up, p.Radius, Color.white);
        }
    }

    /// <summary>Kyūketsuki: blood feeds you, but rest no longer does.</summary>
    public sealed class BloodThirstTechnique : RewardDef
    {
        public override string Id => RewardIds.BloodThirst;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Sede de Sangue";
        public override string Gain => "Seus golpes curam 8% do dano causado; finalizações curam 12.";
        public override string Cost => "A cura ao vencer cada sala cai 15.";
        public override string SourceEnemy => EnemyDefs.Vampiro.displayName;

        public override void Apply(PlayerModifiers m)
        {
            m.lifesteal += 0.08f;
            m.finisherHeal += 12f;
            m.roomClearHealBonus -= 15f;
        }
    }

    /// <summary>Gashadokuro: once per room, a fatal blow leaves you standing at 1.</summary>
    public sealed class StubbornBonesTechnique : RewardDef
    {
        int usedInRoom = -1;

        public override string Id => RewardIds.StubbornBones;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Ossos Teimosos";
        public override string Gain => "Uma vez por sala, um golpe fatal te deixa com 1 de vida (e 1 s sem levar dano).";
        public override string Cost => "-15 de vida máxima.";
        public override string SourceEnemy => EnemyDefs.Esqueleto.displayName;

        public override void Apply(PlayerModifiers m) { m.maxHealthBonus -= 15f; }

        public override bool OnLethalHit(World world)
        {
            if (world.Run == null || usedInRoom == world.Run.RoomIndex) return false;
            usedInRoom = world.Run.RoomIndex;
            world.Fx.Popup("OSSOS TEIMOSOS", world.Player.Position + Vector2.up * 1.2f, EnemyDefs.Esqueleto.color);
            world.Fx.Emit(FxType.Spawn, world.Player.Position, Vector2.up, world.Player.Radius, EnemyDefs.Esqueleto.color);
            return true;
        }
    }

    /// <summary>Macaco Boxeador: a held block loads a counter-punch.</summary>
    public sealed class CounterPunchTechnique : RewardDef
    {
        const float WindowSeconds = 1f;
        float readyUntil = -1f;

        public override string Id => RewardIds.CounterPunch;
        public override RewardKind Kind => RewardKind.Technique;
        public override string Name => "Contragolpe";
        public override string Gain => "Após bloquear (sem ser perfeito), seu próximo golpe em 1 s causa +50% de dano e 2x postura.";
        public override string Cost => "Bloquear desgasta 15% mais a sua postura.";
        public override string SourceEnemy => EnemyDefs.Boxeador.displayName;

        public override void Apply(PlayerModifiers m) { m.blockWearMult *= 1.15f; }

        public override void OnBlock(World world, Enemy attacker)
        {
            readyUntil = world.Run.Elapsed + WindowSeconds;
            world.Player.EmpoweredFrames = Mathf.Max(world.Player.EmpoweredFrames, Mathf.RoundToInt(WindowSeconds * SimClock.TickRate));
        }

        public override void ModifyAttackDamage(World world, Enemy target, ref float damage)
        {
            if (world.Run.Elapsed > readyUntil) return;
            damage *= 1.5f;
        }

        public override void ModifyAttackPosture(World world, Enemy target, ref float posture)
        {
            if (world.Run.Elapsed > readyUntil) return;
            posture *= 2f;
            readyUntil = -1f;
            world.Fx.Popup("CONTRAGOLPE", target.Position + Vector2.up * 0.9f, world.Config.palette.gold);
        }
    }
}
