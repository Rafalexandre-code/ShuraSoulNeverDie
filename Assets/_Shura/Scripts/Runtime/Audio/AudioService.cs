using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Shura
{
    public enum Sfx
    {
        Swing,
        Hit,
        Block,
        PerfectParry,
        PostureBreak,
        Finisher,
        Dodge,
        Hurt,
        Danger,
        FlowUp,
        FlowDown,
        Absorb,
        Death,
        Spawn,
        Chime,
        UiMove,
        UiConfirm,
        Glint,
        Purchase,
        Denied,
        /// <summary>Blade on blade; one of many recorded variants is picked at random.</summary>
        Clash,
        Unsheathe,
        Slice,
        Splatter,
        Crunch,
        SheathClick,
        Chain,
        Pickup,
        /// <summary>Long, low whoosh of a heavy weapon (or a big special strike).</summary>
        SwingHeavy,
        Step,
    }

    /// <summary>
    /// Pooled one-shots and a soundtrack of two moods. "Calm" (koto, shakuhachi, piano) plays
    /// in menus, the lobby and between waves; "Fight" (taiko, koto, guitar) in combat. Each
    /// mood is a shuffled playlist of whole tracks that crossfade into one another, never the
    /// same track twice in a row, and both keep playing underneath so a mood comes back where
    /// it left off. All tracks are loudness-normalized (see Tools/music).
    ///
    /// Sound effects are real recordings (CC0 packs in Resources/Audio/Sfx, see CREDITS.md):
    /// each event draws a random variant, never the same one twice in a row, with slight pitch
    /// variation. Some events also layer the old synthesized sound underneath for its musical
    /// "sting" (the ring of a perfect parry, the thump of a posture break). Anything missing
    /// falls back to the synthesizer, so the game never goes silent.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        const int PoolSize = 24;
        const float MusicFadeSharpness = 1.6f;
        /// <summary>Seconds of overlap when one track of a mood hands over to the next.</summary>
        const float TrackCrossfade = 4f;

        sealed class Bank
        {
            public AudioClip[] samples;
            public AudioClip synth;
            /// <summary>Volume of the synthesized layer when samples exist (0 = samples only).</summary>
            public float synthLayer;
            public float sampleVolume = 1f;
            public int last = -1;
        }

        /// <summary>One mood: a playlist on two sources that take turns, crossfading.</summary>
        sealed class Mood
        {
            public AudioClip[] clips;
            public readonly AudioSource[] decks = new AudioSource[2];
            public int active;
            public int last = -1;
            public float level;
            public float target;
            /// <summary>Per-deck fade inside the mood (the outgoing track fades out, the next in).</summary>
            public readonly float[] fade = new float[2];
        }

        readonly Mood calm = new Mood();
        readonly Mood fight = new Mood();
        AudioSource[] pool;
        Bank[] banks;
        int next;
        GameSettings settings;

        public bool UsingRecordedAudio { get; private set; }

        public void Init(GameSettings gameSettings, bool headless)
        {
            settings = gameSettings;

            banks = new Bank[Enum.GetValues(typeof(Sfx)).Length];
            Set(Sfx.Swing, SfxSynth.Swing(), "swing");
            Set(Sfx.SwingHeavy, SfxSynth.Swing(), "slash");
            Set(Sfx.Hit, SfxSynth.Hit(), "flesh", 0.45f);
            Set(Sfx.Block, SfxSynth.Block(), "block", 0.3f);
            Set(Sfx.PerfectParry, SfxSynth.PerfectParry(), "parry", 0.55f);
            Set(Sfx.PostureBreak, SfxSynth.PostureBreak(), "heavy", 0.7f);
            Set(Sfx.Finisher, SfxSynth.Finisher(), "heavy", 0.75f);
            Set(Sfx.Dodge, SfxSynth.Dodge(), "dodge", 0.6f);
            Set(Sfx.Hurt, SfxSynth.Hurt(), "flesh", 0.7f);
            Set(Sfx.Danger, SfxSynth.Danger());
            Set(Sfx.FlowUp, SfxSynth.FlowUp());
            Set(Sfx.FlowDown, SfxSynth.FlowDown());
            Set(Sfx.Absorb, SfxSynth.Absorb());
            Set(Sfx.Death, SfxSynth.Death(), "splat", 0.6f);
            Set(Sfx.Spawn, SfxSynth.Spawn());
            Set(Sfx.Chime, SfxSynth.Chime());
            Set(Sfx.UiMove, SfxSynth.UiMove());
            Set(Sfx.UiConfirm, SfxSynth.UiConfirm());
            Set(Sfx.Glint, SfxSynth.Glint());
            Set(Sfx.Purchase, SfxSynth.Purchase());
            Set(Sfx.Denied, SfxSynth.Denied());
            Set(Sfx.Clash, SfxSynth.SwordClash(0), "clash", 0.25f);
            Set(Sfx.Unsheathe, SfxSynth.Unsheathe(), "unsheathe");
            Set(Sfx.Slice, SfxSynth.Slice(), "flesh", 0.8f);
            Set(Sfx.Splatter, SfxSynth.Splatter(), "splat", 0.4f);
            Set(Sfx.Crunch, SfxSynth.Crunch(), "crunch", 0.5f);
            Set(Sfx.SheathClick, SfxSynth.SheathClick(), "sheathe");
            Set(Sfx.Chain, SfxSynth.Chain());
            Set(Sfx.Pickup, SfxSynth.Chime(), "pickup", 0.8f);
            Set(Sfx.Step, null, "step");
            banks[(int)Sfx.Slice].sampleVolume = 0.55f;

            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }

            // Headless test runs skip music entirely.
            if (headless) return;
            InitMood(calm, "Calm");
            InitMood(fight, "Fight");
        }

        void InitMood(Mood mood, string folder)
        {
            mood.clips = Resources.LoadAll<AudioClip>("Audio/Music/" + folder);
            if (mood.clips == null || mood.clips.Length == 0) return;
            Array.Sort(mood.clips, (a, b) => string.CompareOrdinal(a.name, b.name));
            for (int i = 0; i < 2; i++)
            {
                AudioSource src = gameObject.AddComponent<AudioSource>();
                src.loop = false;
                src.volume = 0f;
                src.playOnAwake = false;
                mood.decks[i] = src;
            }
            StartTrack(mood, 0);
        }

        /// <summary>A random track of the mood, never the one that just played.</summary>
        void StartTrack(Mood mood, int deck)
        {
            int n = mood.clips.Length;
            int pick = Random.Range(0, n);
            if (n > 1 && pick == mood.last) pick = (pick + 1 + Random.Range(0, n - 1)) % n;
            mood.last = pick;
            AudioSource src = mood.decks[deck];
            src.clip = mood.clips[pick];
            src.time = 0f;
            src.Play();
            mood.active = deck;
            mood.fade[deck] = 0f;
        }

        void Set(Sfx sfx, AudioClip synth, string folder = null, float synthLayer = 0f)
        {
            AudioClip[] samples = folder != null ? Resources.LoadAll<AudioClip>("Audio/Sfx/" + folder) : null;
            if (samples != null && samples.Length > 0)
            {
                Array.Sort(samples, (a, b) => string.CompareOrdinal(a.name, b.name));
                UsingRecordedAudio = true;
            }
            banks[(int)sfx] = new Bank { samples = samples, synth = synth, synthLayer = synthLayer };
        }

        public void Play(Sfx sfx, float volume = 1f, float pitch = 1f, float pitchVariance = 0.06f)
        {
            if (banks == null) return;
            Bank bank = banks[(int)sfx];
            if (bank == null) return;
            float p = pitch * (1f + Random.Range(-pitchVariance, pitchVariance));
            if (bank.samples != null && bank.samples.Length > 0)
            {
                int pick = Random.Range(0, bank.samples.Length);
                if (bank.samples.Length > 1 && pick == bank.last) pick = (pick + 1) % bank.samples.Length;
                bank.last = pick;
                Emit(bank.samples[pick], volume * bank.sampleVolume, p);
                if (bank.synth != null && bank.synthLayer > 0f) Emit(bank.synth, volume * bank.synthLayer, p);
                return;
            }
            if (bank.synth != null) Emit(bank.synth, volume, p);
        }

        /// <summary>Scales one-shots (teammates' moments are quieter than the host's).</summary>
        public float VolumeScale { get; set; } = 1f;

        void Emit(AudioClip clip, float volume, float pitch)
        {
            AudioSource src = pool[next];
            next = (next + 1) % PoolSize;
            src.clip = clip;
            src.volume = volume * settings.sfx * VolumeScale;
            src.pitch = pitch;
            src.Play();
        }

        /// <summary>How loud each mood should be (0..1); they crossfade toward these.</summary>
        public void SetMusicMood(float calmLevel, float fightLevel)
        {
            calm.target = calmLevel;
            fight.target = fightLevel;
        }

        void Update()
        {
            if (settings == null) return;
            float dt = Time.unscaledDeltaTime;
            TickMood(calm, dt);
            TickMood(fight, dt);
        }

        void TickMood(Mood mood, float dt)
        {
            if (mood.clips == null || mood.clips.Length == 0) return;
            mood.level = Mathf.Lerp(mood.level, mood.target, CombatMath.Damp(MusicFadeSharpness, dt));

            // Near the end of the track, the other deck starts the next one and they crossfade.
            AudioSource current = mood.decks[mood.active];
            bool ending = current.clip != null && (!current.isPlaying || current.time >= current.clip.length - TrackCrossfade);
            if (ending) StartTrack(mood, 1 - mood.active);

            float step = dt / TrackCrossfade;
            for (int i = 0; i < 2; i++)
            {
                AudioSource src = mood.decks[i];
                mood.fade[i] = Mathf.MoveTowards(mood.fade[i], i == mood.active ? 1f : 0f, step);
                src.volume = mood.fade[i] * mood.level * settings.music;
                if (i != mood.active && mood.fade[i] <= 0f && src.isPlaying) src.Stop();
            }
        }
    }
}
