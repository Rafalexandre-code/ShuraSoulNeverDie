using UnityEngine;

namespace Shura
{
    // Techniques absorbed from the bestiary (M1.10). Each one turns the enemy's own mechanic
    // into a tool for the player, and every one has a price. Like the original eight, an
    // instance holds per-run state; in co-op the whole party shares the run's techniques.

    /// <summary>Kunoichi: perfect parries answer with a fan of kunai.</summary>
    public sealed class KunaiFanTechnique : TechniqueDef
    {
        public override string Id => RewardIds.KunaiFan;
        protected override string BaseName => "Leque de Kunai";
        public override Mark[] MarkSet => new[] { Mark.Vento, Mark.Sombra };
        protected override string BaseGain => "Parry perfeito lança 3 kunai em leque na direção de quem atacou (6 dano, 8 postura cada).";
        protected override string BaseCost => "Seu parry perfeito causa 15% menos postura.";
        public override string SourceEnemy => EnemyDefs.Kunoichi.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.perfectParryPostureMult *= 0.85f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            Vector2 dir = attacker != null ? attacker.Position - world.Player.Position : world.Player.Facing;
            if (dir.sqrMagnitude < 1e-4f) dir = world.Player.Facing;
            world.FirePlayerKunai(world.Player.Position, dir.normalized, Power > 1f ? 5 : 3, Power > 1f ? 44f : 30f, 6f, 8f);
            Acted(world, attacker != null ? attacker.Position : world.Player.Position, attacker);
        }
    }

    /// <summary>Bombardeiro: a dodge leaves a bomb where you stood.</summary>
    public sealed class ShortFuseTechnique : TechniqueDef
    {
        const float Cooldown = 3f;
        float last = -99f;

        public override string Id => RewardIds.ShortFuse;
        protected override string BaseName => "Pavio Curto";
        public override Mark[] MarkSet => new[] { Mark.Fogo };
        protected override string BaseGain => "Esquivar deixa uma bomba onde você estava (explode em 0,7 s: 12 dano, 18 postura). Uma a cada 3 s.";
        protected override string BaseCost => "-8 de vida máxima.";
        public override string SourceEnemy => EnemyDefs.Bombardeiro.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.maxHealthBonus -= 8f; }

        public override void OnDodge(World world, Vector2 from)
        {
            if (world.Run.Elapsed - last < Cooldown / Power) return;
            last = world.Run.Elapsed;
            world.ScheduleBurst(from, 42, 1.6f, 12f * Power, 18f * Power, World.BlastColor(BlastKind.Bomb), true);
            Acted(world, from, null);
        }
    }

    /// <summary>Tengu: a blade kept sheathed for a moment comes out as a crushing draw.</summary>
    public sealed class IaiDrawTechnique : TechniqueDef
    {
        const float ChargeSeconds = 1f;
        float lastSwing = -99f;
        bool charged;
        int combo;

        public override string Id => RewardIds.IaiDraw;
        protected override string BaseName => "Saque Iai";
        public override Mark[] MarkSet => new[] { Mark.Corte, Mark.Trovao };
        protected override string BaseGain => "Ficar 1 s sem atacar carrega o saque: o próximo golpe causa +80% de dano e 2x postura.";
        protected override string BaseCost => "O 2º e o 3º golpes do combo causam 15% menos dano.";
        public override string SourceEnemy => EnemyDefs.Tengu.displayName;

        public override void OnSwing(World world, int comboIndex, Vector2 direction)
        {
            charged = world.Run.Elapsed - lastSwing >= ChargeSeconds;
            lastSwing = world.Run.Elapsed;
            combo = comboIndex;
            if (!charged) return;
            world.Fx.Popup("IAI", world.Player.Position + Vector2.up * 1f, world.Config.palette.gold);
            Acted(world, world.Player.Position + direction, null);
        }

        public override void ModifyAttackDamage(World world, Enemy target, ref float damage)
        {
            if (charged) damage *= 1f + 0.8f * Power;
            else if (combo > 0 && PaysCost) damage *= 0.85f;
        }

        public override void ModifyAttackPosture(World world, Enemy target, ref float posture)
        {
            if (charged) posture *= 1f + Power;
        }
    }

    /// <summary>Onmyōji: a perfect parry seals the floor under the attacker.</summary>
    public sealed class ExplosiveSealTechnique : TechniqueDef
    {
        public override string Id => RewardIds.ExplosiveSeal;
        protected override string BaseName => "Selo Explosivo";
        public override Mark[] MarkSet => new[] { Mark.Espirito, Mark.Fogo };
        protected override string BaseGain => "Parry perfeito marca o chão sob quem atacou: explode em 0,8 s (14 dano, 22 postura em todos ao redor).";
        protected override string BaseCost => "Seu parry perfeito causa 20% menos postura direta.";
        public override string SourceEnemy => EnemyDefs.Onmyoji.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.perfectParryPostureMult *= 0.8f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            if (attacker == null) return;
            world.ScheduleBurst(attacker.Position, 48, 1.6f * Mathf.Sqrt(Power), 14f * Power, 22f * Power, World.BlastColor(BlastKind.Talisman), true);
            Acted(world, attacker.Position, attacker);
        }
    }

    /// <summary>Xamã: every finisher mends the whole party.</summary>
    public sealed class ShamanBlessingTechnique : TechniqueDef
    {
        const float Heal = 10f;

        public override string Id => RewardIds.ShamanBlessing;
        protected override string BaseName => "Bênção da Xamã";
        public override Mark[] MarkSet => new[] { Mark.Espirito, Mark.Sangue };
        protected override string BaseGain => "Toda finalização cura 10 de vida de TODO o grupo e zera a sua postura.";
        protected override string BaseCost => "-10% de dano.";
        public override string SourceEnemy => EnemyDefs.Curandeira.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.damageMult *= 0.9f; }

        public override void OnFinisher(World world, Enemy target)
        {
            foreach (PlayerController p in world.Players)
            {
                if (!p.IsAlive) continue;
                p.Heal(Heal * Power);
                world.Fx.Emit(FxType.Heal, p.Position, Vector2.up, p.Radius, EnemyDefs.Curandeira.color);
            }
            world.Player.Posture.Reset(0f);
            Acted(world, target.Position, null);
        }
    }

    /// <summary>Chōchin-obake: what you kill explodes.</summary>
    public sealed class LivingLanternTechnique : TechniqueDef
    {
        public override string Id => RewardIds.LivingLantern;
        protected override string BaseName => "Lanterna Viva";
        public override Mark[] MarkSet => new[] { Mark.Fogo, Mark.Espirito };
        protected override string BaseGain => "Todo inimigo que cai explode após 0,4 s (raio 1,8: 12 dano, 18 postura nos outros inimigos).";
        protected override string BaseCost => "Você recebe 10% mais dano.";
        public override string SourceEnemy => EnemyDefs.Lanterna.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.damageTakenMult *= 1.1f; }

        public override void OnKill(World world, Enemy enemy)
        {
            world.ScheduleBurst(enemy.Position, 24, 1.8f * Mathf.Sqrt(Power), 12f * Power, 18f * Power, World.BlastColor(BlastKind.Fire), true);
            Acted(world, enemy.Position, null);
        }
    }

    /// <summary>Onryō: a perfect parry makes you untouchable for a heartbeat.</summary>
    public sealed class EtherealBodyTechnique : TechniqueDef
    {
        public override string Id => RewardIds.EtherealBody;
        protected override string BaseName => "Corpo Etéreo";
        public override Mark[] MarkSet => new[] { Mark.Espirito, Mark.Sombra };
        protected override string BaseGain => "Após um parry perfeito, nada te acerta por 0,5 s.";
        protected override string BaseCost => "Suas esquivas recarregam 25% mais devagar.";
        public override string SourceEnemy => EnemyDefs.Onryo.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.dodgeRechargeMult *= 1.25f; }

        public override void OnPerfectParry(World world, Enemy attacker)
        {
            world.Player.GrantInvulnerable(Mathf.RoundToInt(30 * Power));
            Acted(world, world.Player.Position, attacker);
            world.Fx.Emit(FxType.Phase, world.Player.Position, Vector2.up, world.Player.Radius, EnemyDefs.Onryo.color);
        }
    }

    /// <summary>Kappa: a perfect dodge gives the charge back, and a sip of health.</summary>
    public sealed class KappaCurrentTechnique : TechniqueDef
    {
        public override string Id => RewardIds.KappaCurrent;
        protected override string BaseName => "Correnteza do Kappa";
        public override Mark[] MarkSet => new[] { Mark.Vento, Mark.Sangue };
        protected override string BaseGain => "Esquiva perfeita devolve a carga de esquiva e cura 4.";
        protected override string BaseCost => "Suas esquivas são 15% mais curtas.";
        public override string SourceEnemy => EnemyDefs.Kappa.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.dodgeDistanceMult *= 0.85f; }

        public override void OnPerfectDodge(World world, Enemy attacker)
        {
            world.Player.RefundDodge();
            world.Player.Heal(4f * Power);
            Acted(world, world.Player.Position, attacker);
        }
    }

    /// <summary>Oni Ciclope: enemies pinned against a wall or statue break twice as fast.</summary>
    public sealed class OniCrushTechnique : TechniqueDef
    {
        public override string Id => RewardIds.OniCrush;
        protected override string BaseName => "Golpe do Oni";
        public override Mark[] MarkSet => new[] { Mark.Guarda, Mark.Corte };
        protected override string BaseGain => "Golpes em inimigos encostados numa parede ou estátua causam 2x postura.";
        protected override string BaseCost => "Você anda 8% mais devagar.";
        public override string SourceEnemy => EnemyDefs.Ciclope.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.moveSpeedMult *= 0.92f; }

        public override void ModifyAttackPosture(World world, Enemy target, ref float posture)
        {
            if (world.Layout == null || world.Layout.Fits(target.Position, target.Radius + 0.45f)) return;
            posture *= 1f + Power;
            world.Fx.Popup("CONTRA A PAREDE", target.Position + Vector2.up * 1f, world.Config.palette.gold);
            Acted(world, target.Position, target);
        }
    }

    /// <summary>Lodo: every fallen enemy feeds you.</summary>
    public sealed class SlimeHungerTechnique : TechniqueDef
    {
        const float Heal = 5f;

        public override string Id => RewardIds.SlimeHunger;
        protected override string BaseName => "Fome do Lodo";
        public override Mark[] MarkSet => new[] { Mark.Sangue };
        protected override string BaseGain => "Cada inimigo que cai cura 5 de vida (lacaios não contam).";
        protected override string BaseCost => "-10 de vida máxima.";
        public override string SourceEnemy => EnemyDefs.Lodo.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.maxHealthBonus -= 10f; }

        public override void OnKill(World world, Enemy enemy)
        {
            if (enemy.Def.minion || !world.Player.IsAlive) return;
            world.Player.Heal(Heal * Power);
            world.Fx.Popup("+" + Mathf.RoundToInt(Heal * Power), world.Player.Position + Vector2.up * 0.8f, world.Config.palette.health);
            Acted(world, enemy.Position, null);
        }
    }

    /// <summary>Bárbaro: the closer to death, the harder you hit.</summary>
    public sealed class FrenzyTechnique : TechniqueDef
    {
        public override string Id => RewardIds.Frenzy;
        protected override string BaseName => "Fúria do Bárbaro";
        public override Mark[] MarkSet => new[] { Mark.Sangue, Mark.Fogo };
        protected override string BaseGain => "Abaixo de 50% de vida, seus golpes causam +40% de dano.";
        protected override string BaseCost => "Acima de 50% de vida, seus golpes causam 10% menos dano.";
        public override string SourceEnemy => EnemyDefs.Berserker.displayName;

        public override void ModifyAttackDamage(World world, Enemy target, ref float damage)
        {
            PlayerController p = world.Player;
            bool low = p.Health < p.MaxHealth * 0.5f;
            if (low) damage *= 1f + 0.4f * Power;
            else if (PaysCost) damage *= 0.9f;
            if (low) Acted(world, target.Position, target);
        }
    }

    /// <summary>Ninja do Trovão: the last blow of every combo is repeated by a shadow.</summary>
    public sealed class ShadowCopyTechnique : TechniqueDef
    {
        public override string Id => RewardIds.ShadowCopy;
        protected override string BaseName => "Cópia de Sombra";
        public override Mark[] MarkSet => new[] { Mark.Sombra, Mark.Trovao };
        protected override string BaseGain => "O 3º golpe do combo é repetido por uma cópia 0,25 s depois (8 dano, 10 postura).";
        protected override string BaseCost => "-8 de vida máxima.";
        public override string SourceEnemy => EnemyDefs.Raijin.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.maxHealthBonus -= 8f; }

        public override void OnSwing(World world, int comboIndex, Vector2 direction)
        {
            if (comboIndex < 2) return;
            world.ScheduleBurst(world.Player.Position + direction * 1.2f, 15, 1.3f, 8f * Power, 10f * Power, EnemyDefs.Raijin.color);
            Acted(world, world.Player.Position + direction * 1.2f, null);
        }
    }

    /// <summary>Olho Vigia: a perfect dodge blinks you behind the attacker.</summary>
    public sealed class SentinelBlinkTechnique : TechniqueDef
    {
        public override string Id => RewardIds.SentinelBlink;
        protected override string BaseName => "Piscar do Vigia";
        public override Mark[] MarkSet => new[] { Mark.Sombra, Mark.Trovao };
        protected override string BaseGain => "Esquiva perfeita te teleporta para as costas de quem atacou, já virado para ele.";
        protected override string BaseCost => "Suas esquivas recarregam 20% mais devagar.";
        public override string SourceEnemy => EnemyDefs.Olho.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.dodgeRechargeMult *= 1.2f; }

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
            if (Power > 1f) attacker.ApplyPosture(15f);
            Acted(world, attacker.Position, attacker);
        }
    }

    /// <summary>Kyūketsuki: blood feeds you, but rest no longer does.</summary>
    public sealed class BloodThirstTechnique : TechniqueDef
    {
        public override string Id => RewardIds.BloodThirst;
        protected override string BaseName => "Sede de Sangue";
        public override Mark[] MarkSet => new[] { Mark.Sangue };
        protected override string BaseGain => "Seus golpes curam 8% do dano causado; finalizações curam 12.";
        protected override string BaseCost => "A cura ao vencer cada sala cai 15.";
        public override string SourceEnemy => EnemyDefs.Vampiro.displayName;

        protected override void ApplyGain(PlayerModifiers m)
        {
            m.lifesteal += 0.08f * Power;
            m.finisherHeal += 12f * Power;
        }

        protected override void ApplyCost(PlayerModifiers m) { m.roomClearHealBonus -= 15f; }

        public override void OnFinisher(World world, Enemy target) => Acted(world, target.Position, null);
    }

    /// <summary>Gashadokuro: once per room, a fatal blow leaves you standing at 1.</summary>
    public sealed class StubbornBonesTechnique : TechniqueDef
    {
        int usedInRoom = -1;

        public override string Id => RewardIds.StubbornBones;
        protected override string BaseName => "Ossos Teimosos";
        public override Mark[] MarkSet => new[] { Mark.Guarda, Mark.Espirito };
        protected override string BaseGain => "Uma vez por sala, um golpe fatal te deixa com 1 de vida (e 1 s sem levar dano).";
        protected override string BaseCost => "-15 de vida máxima.";
        public override string SourceEnemy => EnemyDefs.Esqueleto.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.maxHealthBonus -= 15f; }

        public override bool OnLethalHit(World world)
        {
            if (world.Run == null || usedInRoom == world.Run.RoomIndex) return false;
            usedInRoom = world.Run.RoomIndex;
            world.Fx.Popup("OSSOS TEIMOSOS", world.Player.Position + Vector2.up * 1.2f, EnemyDefs.Esqueleto.color);
            world.Fx.Emit(FxType.Spawn, world.Player.Position, Vector2.up, world.Player.Radius, EnemyDefs.Esqueleto.color);
            if (Power > 1f && world.Player.Build != null) world.Player.Build.PendingReviveHealth = Mathf.Max(world.Player.Build.PendingReviveHealth, 20f);
            Acted(world, world.Player.Position, null);
            return true;
        }
    }

    /// <summary>Macaco Boxeador: a held block loads a counter-punch.</summary>
    public sealed class CounterPunchTechnique : TechniqueDef
    {
        const float WindowSeconds = 1f;
        float readyUntil = -1f;

        public override string Id => RewardIds.CounterPunch;
        protected override string BaseName => "Contragolpe";
        public override Mark[] MarkSet => new[] { Mark.Guarda, Mark.Corte };
        protected override string BaseGain => "Após bloquear (sem ser perfeito), seu próximo golpe em 1 s causa +50% de dano e 2x postura.";
        protected override string BaseCost => "Bloquear desgasta 15% mais a sua postura.";
        public override string SourceEnemy => EnemyDefs.Boxeador.displayName;

        protected override void ApplyCost(PlayerModifiers m) { m.blockWearMult *= 1.15f; }

        public override void OnBlock(World world, Enemy attacker)
        {
            readyUntil = world.Run.Elapsed + WindowSeconds;
            world.Player.EmpoweredFrames = Mathf.Max(world.Player.EmpoweredFrames, Mathf.RoundToInt(WindowSeconds * SimClock.TickRate));
        }

        public override void ModifyAttackDamage(World world, Enemy target, ref float damage)
        {
            if (world.Run.Elapsed > readyUntil) return;
            damage *= 1f + 0.5f * Power;
        }

        public override void ModifyAttackPosture(World world, Enemy target, ref float posture)
        {
            if (world.Run.Elapsed > readyUntil) return;
            posture *= 1f + Power;
            readyUntil = -1f;
            world.Fx.Popup("CONTRAGOLPE", target.Position + Vector2.up * 0.9f, world.Config.palette.gold);
            Acted(world, target.Position, target);
        }
    }
}
