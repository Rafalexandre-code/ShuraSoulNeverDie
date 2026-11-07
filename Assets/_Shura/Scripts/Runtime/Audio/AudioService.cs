using System;
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
    }

    public enum MusicLayer
    {
        Drone = 0,
        Drums = 1,
        Strings = 2,
        Choir = 3,
    }

    /// <summary>
    /// Pooled one-shots with slight pitch variation (so repeats never sound robotic) and four
    /// synchronized music layers whose volumes follow combat state and Flow.
    /// </summary>
    public sealed class AudioService : MonoBehaviour
    {
        const int PoolSize = 20;
        const float MusicFadeSharpness = 2.5f;

        readonly AudioSource[] music = new AudioSource[4];
        readonly float[] musicTarget = new float[4];
        AudioSource[] pool;
        AudioClip[] clips;
        int next;
        GameSettings settings;

        public void Init(GameSettings gameSettings, bool headless)
        {
            settings = gameSettings;

            clips = new AudioClip[Enum.GetValues(typeof(Sfx)).Length];
            clips[(int)Sfx.Swing] = SfxSynth.Swing();
            clips[(int)Sfx.Hit] = SfxSynth.Hit();
            clips[(int)Sfx.Block] = SfxSynth.Block();
            clips[(int)Sfx.PerfectParry] = SfxSynth.PerfectParry();
            clips[(int)Sfx.PostureBreak] = SfxSynth.PostureBreak();
            clips[(int)Sfx.Finisher] = SfxSynth.Finisher();
            clips[(int)Sfx.Dodge] = SfxSynth.Dodge();
            clips[(int)Sfx.Hurt] = SfxSynth.Hurt();
            clips[(int)Sfx.Danger] = SfxSynth.Danger();
            clips[(int)Sfx.FlowUp] = SfxSynth.FlowUp();
            clips[(int)Sfx.FlowDown] = SfxSynth.FlowDown();
            clips[(int)Sfx.Absorb] = SfxSynth.Absorb();
            clips[(int)Sfx.Death] = SfxSynth.Death();
            clips[(int)Sfx.Spawn] = SfxSynth.Spawn();
            clips[(int)Sfx.Chime] = SfxSynth.Chime();
            clips[(int)Sfx.UiMove] = SfxSynth.UiMove();
            clips[(int)Sfx.UiConfirm] = SfxSynth.UiConfirm();
            clips[(int)Sfx.Glint] = SfxSynth.Glint();
            clips[(int)Sfx.Purchase] = SfxSynth.Purchase();
            clips[(int)Sfx.Denied] = SfxSynth.Denied();

            pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                pool[i] = gameObject.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
            }

            // Headless test runs skip the (comparatively slow) music synthesis.
            if (headless) return;
            AudioClip[] loops = { SfxSynth.MusicDrone(), SfxSynth.MusicDrums(), SfxSynth.MusicStrings(), SfxSynth.MusicChoir() };
            double start = AudioSettings.dspTime + 0.2;
            for (int i = 0; i < loops.Length; i++)
            {
                AudioSource src = gameObject.AddComponent<AudioSource>();
                src.clip = loops[i];
                src.loop = true;
                src.volume = 0f;
                src.playOnAwake = false;
                src.PlayScheduled(start);
                music[i] = src;
            }
        }

        public void Play(Sfx sfx, float volume = 1f, float pitch = 1f, float pitchVariance = 0.06f)
        {
            if (clips == null) return;
            AudioClip clip = clips[(int)sfx];
            if (clip == null) return;
            AudioSource src = pool[next];
            next = (next + 1) % PoolSize;
            src.clip = clip;
            src.volume = volume * settings.sfx;
            src.pitch = pitch * (1f + Random.Range(-pitchVariance, pitchVariance));
            src.Play();
        }

        public void SetMusicTargets(float drone, float drums, float strings, float choir)
        {
            musicTarget[(int)MusicLayer.Drone] = drone;
            musicTarget[(int)MusicLayer.Drums] = drums;
            musicTarget[(int)MusicLayer.Strings] = strings;
            musicTarget[(int)MusicLayer.Choir] = choir;
        }

        void Update()
        {
            if (settings == null) return;
            float k = CombatMath.Damp(MusicFadeSharpness, Time.unscaledDeltaTime);
            for (int i = 0; i < music.Length; i++)
            {
                if (music[i] == null) continue;
                music[i].volume = Mathf.Lerp(music[i].volume, musicTarget[i] * settings.music, k);
            }
        }
    }
}
