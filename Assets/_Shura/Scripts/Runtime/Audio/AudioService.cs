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

    public enum MusicLayer
    {
        Drone = 0,
        Drums = 1,
        Strings = 2,
        Choir = 3,
        /// <summary>Bass line that rides with the drums.</summary>
        Bass = 4,
    }

    /// <summary>
    /// Pooled one-shots and music that follows combat state and Flow: by default three
    /// Ninja Adventure tracks crossfading (calm / fight / intense); optionally "Samurai
    /// Nights" as five synchronized layers (Settings).
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
        const float MusicFadeSharpness = 2.5f;
        const int LayerCount = 5;

        sealed class Bank
        {
            public AudioClip[] samples;
            public AudioClip synth;
            /// <summary>Volume of the synthesized layer when samples exist (0 = samples only).</summary>
            public float synthLayer;
            public float sampleVolume = 1f;
            public int last = -1;
        }

        readonly AudioSource[] music = new AudioSource[LayerCount];
        readonly float[] musicTarget = new float[LayerCount];
        /// <summary>Ninja Adventure tracks: calm (exploration, menus), fight, intense (high Flow).</summary>
        readonly AudioSource[] tracks = new AudioSource[3];
        readonly float[] trackTarget = new float[3];
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
            string[] trackNames = { "calm", "fight", "intense" };
            for (int i = 0; i < tracks.Length; i++)
            {
                AudioClip clip = LoadMusic("NA/" + trackNames[i]);
                if (clip == null) continue;
                AudioSource src = gameObject.AddComponent<AudioSource>();
                src.clip = clip;
                src.loop = true;
                src.volume = 0f;
                src.playOnAwake = false;
                src.Play();
                tracks[i] = src;
            }
            AudioClip[] loops =
            {
                LoadMusic("drone") ?? SfxSynth.MusicDrone(),
                LoadMusic("drums") ?? SfxSynth.MusicDrums(),
                LoadMusic("strings") ?? SfxSynth.MusicStrings(),
                LoadMusic("choir") ?? SfxSynth.MusicChoir(),
                LoadMusic("bass"),
            };
            // All stems are whole multiples of one 4-bar loop, so starting them together keeps them locked.
            double start = AudioSettings.dspTime + 0.3;
            for (int i = 0; i < loops.Length; i++)
            {
                if (loops[i] == null) continue;
                AudioSource src = gameObject.AddComponent<AudioSource>();
                src.clip = loops[i];
                src.loop = true;
                src.volume = 0f;
                src.playOnAwake = false;
                src.PlayScheduled(start);
                music[i] = src;
            }
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

        static AudioClip LoadMusic(string name)
        {
            return Resources.Load<AudioClip>("Audio/Music/" + name);
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

        public void SetMusicTargets(float drone, float drums, float strings, float choir)
        {
            musicTarget[(int)MusicLayer.Drone] = drone;
            musicTarget[(int)MusicLayer.Drums] = drums;
            musicTarget[(int)MusicLayer.Strings] = strings;
            musicTarget[(int)MusicLayer.Choir] = choir;
            musicTarget[(int)MusicLayer.Bass] = drums * 0.8f;

            // The same intent drives the full-length tracks: calm outside combat, the fight
            // track in combat, the intense one once Flow brings the melody in (level 2+).
            bool combat = drums > 0.5f;
            bool intense = combat && strings >= 0.55f;
            trackTarget[0] = combat ? 0f : Mathf.Max(drone, choir);
            trackTarget[1] = combat && !intense ? 0.75f : 0f;
            trackTarget[2] = intense ? 0.8f : 0f;
        }

        bool UseTracks => !settings.samuraiNights && tracks[0] != null;

        void Update()
        {
            if (settings == null) return;
            float k = CombatMath.Damp(MusicFadeSharpness, Time.unscaledDeltaTime);
            bool useTracks = UseTracks;
            for (int i = 0; i < music.Length; i++)
            {
                if (music[i] == null) continue;
                float target = useTracks ? 0f : musicTarget[i];
                music[i].volume = Mathf.Lerp(music[i].volume, target * settings.music, k);
            }
            for (int i = 0; i < tracks.Length; i++)
            {
                if (tracks[i] == null) continue;
                float target = useTracks ? trackTarget[i] : 0f;
                tracks[i].volume = Mathf.Lerp(tracks[i].volume, target * settings.music, k);
            }
        }
    }
}
