using System;
using UnityEngine;

namespace Shura
{
    /// <summary>
    /// Placeholder sound design synthesized at startup (no audio files). Each function is a
    /// small recipe; real recordings can replace any clip later through AudioService.
    /// Music loops are exactly 6 s (8 beats at 80 BPM) with whole-cycle frequencies so they
    /// loop seamlessly and stay in sync when layered.
    /// </summary>
    public static class SfxSynth
    {
        public const int Rate = 44100;
        public const float LoopSeconds = 6f;
        const double TwoPi = Math.PI * 2.0;

        static readonly System.Random NoiseRng = new System.Random(1337);

        static float Noise() => (float)(NoiseRng.NextDouble() * 2.0 - 1.0);
        static float Sin(double frequency, double t) => (float)Math.Sin(TwoPi * frequency * t);
        static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];
        static float Env(float t, float attack, float decay) => t < attack ? t / attack : Mathf.Exp(-(t - attack) / decay);

        // ── One-shots ───────────────────────────────────────────────────────────────────

        public static AudioClip Swing()
        {
            float[] d = Buffer(0.16f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float sweep = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.16f));
                lp += (0.04f + 0.3f * sweep) * (Noise() - lp);
                d[i] = lp * Env(t, 0.03f, 0.05f);
            }
            return OneShot("swing", d, 0.6f);
        }

        public static AudioClip Hit()
        {
            float[] d = Buffer(0.25f);
            double phase = 0;
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 45f + 85f * Mathf.Exp(-t / 0.04f);
                phase += TwoPi * f / Rate;
                float body = (float)Math.Sin(phase) * Env(t, 0.002f, 0.08f);
                lp += 0.25f * (Noise() - lp);
                d[i] = body + lp * Env(t, 0.001f, 0.018f) * 0.9f;
            }
            return OneShot("hit", d, 0.9f);
        }

        public static AudioClip Block()
        {
            return Partials("block", 0.25f, new[] { 420f, 967f, 1580f }, new[] { 1f, 0.6f, 0.35f },
                            new[] { 0.07f, 0.045f, 0.03f }, 0.6f, 0.004f, 0.7f);
        }

        public static AudioClip PerfectParry()
        {
            return Partials("perfect_parry", 0.8f,
                            new[] { 1318f, 1979f, 2641f, 3520f, 4190f, 5274f },
                            new[] { 1f, 0.75f, 0.55f, 0.4f, 0.3f, 0.2f },
                            new[] { 0.42f, 0.3f, 0.22f, 0.15f, 0.1f, 0.07f }, 1.2f, 0.006f, 0.85f);
        }

        public static AudioClip PostureBreak()
        {
            float[] f = { 98f, 147.6f, 197.3f, 263.1f, 331f };
            float[] a = { 1f, 0.8f, 0.6f, 0.4f, 0.3f };
            float[] dec = { 0.7f, 0.5f, 0.4f, 0.3f, 0.2f };
            float[] d = Buffer(1.1f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float wobble = 1f + 0.004f * Mathf.Sin(2f * Mathf.PI * 5f * t);
                float s = 0f;
                for (int k = 0; k < f.Length; k++) s += a[k] * Sin(f[k] * wobble, t) * Mathf.Exp(-t / dec[k]);
                lp += 0.5f * (Noise() - lp);
                d[i] = s + lp * Env(t, 0.001f, 0.06f) * 0.8f;
            }
            return OneShot("posture_break", d, 0.95f);
        }

        public static AudioClip Finisher()
        {
            float[] d = Buffer(1.0f);
            double phase = 0;
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 35f + 90f * Mathf.Exp(-t / 0.05f);
                phase += TwoPi * f / Rate;
                float thump = (float)Math.Sin(phase) * Env(t, 0.002f, 0.2f);
                lp += (0.05f + 0.4f * Mathf.Exp(-t / 0.05f)) * (Noise() - lp);
                float slash = lp * Env(t, 0.005f, 0.07f) * 0.7f;
                float gong = (Sin(65f, t) + 0.6f * Sin(98f, t)) * Mathf.Exp(-t / 0.6f) * 0.4f;
                d[i] = thump + slash + gong;
            }
            return OneShot("finisher", d, 1f);
        }

        public static AudioClip Dodge()
        {
            float[] d = Buffer(0.2f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (0.05f + 0.1f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 0.2f))) * (Noise() - lp);
                d[i] = lp * Env(t, 0.05f, 0.05f);
            }
            return OneShot("dodge", d, 0.4f);
        }

        public static AudioClip Hurt()
        {
            float[] d = Buffer(0.32f);
            double phase = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float f = 90f + 70f * Mathf.Exp(-t / 0.08f);
                phase += f / Rate;
                float saw = (float)(2.0 * (phase - Math.Floor(phase)) - 1.0);
                float grit = (float)Math.Tanh(saw * 2.5f) * Env(t, 0.002f, 0.09f);
                float thump = Sin(70f, t) * Env(t, 0.002f, 0.1f);
                d[i] = grit * 0.6f + thump;
            }
            return OneShot("hurt", d, 0.8f);
        }

        /// <summary>The danger cue must be unmistakable and unlike any other sound in the game.</summary>
        public static AudioClip Danger()
        {
            float[] d = Buffer(0.5f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float vib = 1f + 0.006f * Mathf.Sin(2f * Mathf.PI * 28f * t);
                float shing = (Sin(2093f * vib, t) + 0.7f * Sin(3136f * vib, t)) * Env(t, 0.004f, 0.18f);
                float accent = Sin(196f, t) * Env(t, 0.002f, 0.06f) * 0.7f;
                float n = Noise();
                lp += 0.2f * (n - lp);
                d[i] = shing + accent + (n - lp) * Env(t, 0.0005f, 0.005f);
            }
            return OneShot("danger", d, 0.8f);
        }

        public static AudioClip FlowUp() => Notes("flow_up", 0.7f, new[] { 880f, 1108.73f, 1318.51f }, 0.07f, 0.2f, 0.6f);
        public static AudioClip FlowDown() => Notes("flow_down", 0.5f, new[] { 659.25f, 523.25f, 440f }, 0.07f, 0.12f, 0.45f);
        public static AudioClip Chime() => Notes("chime", 0.6f, new[] { 1568f, 2349f }, 0.0f, 0.18f, 0.55f);
        public static AudioClip UiMove() => Notes("ui_move", 0.06f, new[] { 1200f }, 0f, 0.012f, 0.3f);
        public static AudioClip UiConfirm() => Notes("ui_confirm", 0.18f, new[] { 880f, 1320f }, 0.05f, 0.04f, 0.4f);

        /// <summary>
        /// Steel on steel: a sharp transient, a dull body "thunk" and an inharmonic ring that
        /// wobbles slightly. Three variants with different partials so repeats never match.
        /// </summary>
        public static AudioClip SwordClash(int variant)
        {
            float[][] sets =
            {
                new[] { 1180f, 1720f, 2390f, 3150f, 4470f, 6020f },
                new[] { 980f, 1530f, 2210f, 2960f, 3880f, 5310f },
                new[] { 1340f, 1890f, 2640f, 3420f, 5010f, 6650f },
            };
            float[] amps = { 1f, 0.8f, 0.65f, 0.5f, 0.35f, 0.2f };
            float[] decays = { 0.24f, 0.18f, 0.13f, 0.09f, 0.06f, 0.04f };
            float[] f = sets[Mathf.Abs(variant) % sets.Length];

            float[] d = Buffer(0.65f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float bend = 1f + 0.02f * Mathf.Exp(-t / 0.01f);
                float ring = 0f;
                for (int k = 0; k < f.Length; k++) ring += amps[k] * Sin(f[k] * bend, t) * Mathf.Exp(-t / decays[k]);
                ring *= 1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 33f * t);
                float n = Noise();
                lp += 0.35f * (n - lp);
                float scrape = (n - lp) * Env(t, 0.0002f, 0.009f) * 1.6f;
                float thunk = Sin(160f * Mathf.Exp(-t / 0.05f), t) * Env(t, 0.001f, 0.025f) * 0.8f;
                d[i] = ring + scrape + thunk;
            }
            return OneShot("clash_" + variant, d, 0.95f);
        }

        /// <summary>Short bright "tink": the timing ring closed, answer now.</summary>
        public static AudioClip Glint()
        {
            return Partials("glint", 0.18f, new[] { 2637f, 3951f, 5274f }, new[] { 1f, 0.6f, 0.3f },
                            new[] { 0.06f, 0.045f, 0.03f }, 0.4f, 0.002f, 0.6f);
        }

        public static AudioClip Purchase() => Notes("purchase", 0.7f, new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.06f, 0.25f, 0.6f);

        public static AudioClip Denied()
        {
            float[] d = Buffer(0.28f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float gate = t < 0.1f || (t > 0.14f && t < 0.24f) ? 1f : 0f;
                float square = Mathf.Sign(Sin(150f, t)) * 0.5f + Sin(150f, t) * 0.5f;
                d[i] = square * gate * Mathf.Exp(-t / 0.3f);
            }
            return OneShot("denied", d, 0.35f);
        }

        public static AudioClip Absorb()
        {
            float[] d = Buffer(1.3f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float s = 0f;
                for (int k = 1; k <= 8; k++) s += Sin(500f * k * (1f + 0.5f * t), t) / k;
                float n = Noise();
                lp += 0.3f * (n - lp);
                d[i] = (s + (n - lp) * 0.1f) * Env(t, 0.25f, 0.35f);
            }
            return OneShot("absorb", d, 0.7f);
        }

        public static AudioClip Death()
        {
            float[] d = Buffer(0.5f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                lp += (0.3f * Mathf.Exp(-t / 0.15f) + 0.02f) * (Noise() - lp);
                d[i] = lp * Env(t, 0.003f, 0.15f) + Sin(70f * Mathf.Exp(-t / 0.3f), t) * Env(t, 0.003f, 0.15f);
            }
            return OneShot("death", d, 0.7f);
        }

        public static AudioClip Spawn()
        {
            float[] d = Buffer(0.6f);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float env = Env(t, 0.35f, 0.1f);
                lp += 0.05f * (Noise() - lp);
                d[i] = (Sin(180f + 260f * t * t, t) + lp * 0.3f) * env;
            }
            return OneShot("spawn", d, 0.4f);
        }

        // ── Music layers ────────────────────────────────────────────────────────────────

        public static AudioClip MusicDrone()
        {
            float[] d = Buffer(LoopSeconds);
            for (int i = 0; i < d.Length; i++)
            {
                double t = i / (double)Rate;
                float lfo = 0.75f + 0.25f * (float)Math.Sin(TwoPi * t / LoopSeconds);
                d[i] = (0.5f * Sin(55.0, t) + 0.32f * Sin(82.5, t) + 0.18f * Sin(110.0, t) + 0.06f * Sin(165.0, t)) * lfo;
            }
            return Loop("music_drone", d, 0.5f);
        }

        public static AudioClip MusicDrums()
        {
            float[] d = Buffer(LoopSeconds);
            const float beat = 0.75f;
            float[] hits = { 0f, 1.5f, 2f, 3f, 4f, 5.5f, 6f, 6.5f, 7f };
            float[] vel = { 1f, 0.6f, 0.85f, 0.55f, 1f, 0.6f, 0.85f, 0.5f, 0.7f };
            int taikoLength = Mathf.CeilToInt(0.9f * Rate);
            for (int h = 0; h < hits.Length; h++)
            {
                int start = Mathf.RoundToInt(hits[h] * beat * Rate);
                double phase = 0;
                for (int j = 0; j < taikoLength; j++)
                {
                    float t = j / (float)Rate;
                    float f = 42f + 40f * Mathf.Exp(-t / 0.03f);
                    phase += TwoPi * f / Rate;
                    float s = (float)Math.Sin(phase) * Env(t, 0.002f, 0.28f) + Noise() * Env(t, 0.0005f, 0.01f) * 0.25f;
                    d[(start + j) % d.Length] += s * vel[h];
                }
            }
            float[] rims = { 0.5f, 2.5f, 4.5f, 7.5f };
            int rimLength = Mathf.CeilToInt(0.1f * Rate);
            foreach (float r in rims)
            {
                int start = Mathf.RoundToInt(r * beat * Rate);
                float lp = 0f;
                for (int j = 0; j < rimLength; j++)
                {
                    float t = j / (float)Rate;
                    float n = Noise();
                    lp += 0.3f * (n - lp);
                    d[(start + j) % d.Length] += (n - lp) * Env(t, 0.0005f, 0.02f) * 0.25f;
                }
            }
            return Loop("music_drums", d, 0.7f);
        }

        public static AudioClip MusicStrings()
        {
            float[] d = Buffer(LoopSeconds);
            float[] notes = { 110f, 110f, 130.81f, 110f, 164.81f, 110f, 146.83f, 130.81f,
                              110f, 110f, 130.81f, 110f, 196f, 164.81f, 146.83f, 130.81f };
            const float eighth = 0.375f;
            int noteLength = Mathf.CeilToInt((eighth + 0.12f) * Rate);
            for (int n = 0; n < notes.Length; n++)
            {
                int start = Mathf.RoundToInt(n * eighth * Rate);
                for (int j = 0; j < noteLength; j++)
                {
                    float t = j / (float)Rate;
                    float s = 0f;
                    for (int k = 1; k <= 6; k++) s += (Sin(notes[n] * k, t) + Sin((notes[n] + 0.6f) * k, t)) / k;
                    d[(start + j) % d.Length] += s * Env(t, 0.02f, 0.22f);
                }
            }
            return Loop("music_strings", d, 0.45f);
        }

        public static AudioClip MusicChoir()
        {
            float[] d = Buffer(LoopSeconds);
            double[] f = { 220.0, 261.5, 329.5, 440.0 };
            float[] amp = { 1f, 0.8f, 0.7f, 0.35f };
            const double detune = 1.0 / LoopSeconds;
            for (int i = 0; i < d.Length; i++)
            {
                double t = i / (double)Rate;
                float swell = 0.65f + 0.35f * (float)Math.Sin(TwoPi * t / LoopSeconds - Math.PI * 0.5);
                float s = 0f;
                for (int v = 0; v < f.Length; v++)
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        double fv = f[v] + side * detune;
                        s += amp[v] * (Sin(fv, t) + 0.45f * Sin(fv * 2.0, t) + 0.2f * Sin(fv * 3.0, t));
                    }
                }
                d[i] = s * swell;
            }
            return Loop("music_choir", d, 0.4f);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────────────

        static AudioClip Partials(string name, float seconds, float[] freqs, float[] amps, float[] decays,
                                  float clickAmount, float clickDecay, float peak)
        {
            float[] d = Buffer(seconds);
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)Rate;
                float s = 0f;
                for (int k = 0; k < freqs.Length; k++) s += amps[k] * Sin(freqs[k], t) * Mathf.Exp(-t / decays[k]);
                float n = Noise();
                lp += 0.2f * (n - lp);
                d[i] = s + (n - lp) * Env(t, 0.0003f, clickDecay) * clickAmount;
            }
            return OneShot(name, d, peak);
        }

        static AudioClip Notes(string name, float seconds, float[] freqs, float spacing, float decay, float peak)
        {
            float[] d = Buffer(seconds);
            for (int n = 0; n < freqs.Length; n++)
            {
                int start = Mathf.RoundToInt(n * spacing * Rate);
                for (int i = start; i < d.Length; i++)
                {
                    float t = (i - start) / (float)Rate;
                    float bell = Sin(freqs[n], t) + 0.3f * Sin(freqs[n] * 2f, t) + 0.15f * Sin(freqs[n] * 3f, t);
                    d[i] += bell * Env(t, 0.003f, decay);
                }
            }
            return OneShot(name, d, peak);
        }

        static void Normalize(float[] data, float peak)
        {
            float max = 0.0001f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            float k = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= k;
        }

        static AudioClip OneShot(string name, float[] data, float peak)
        {
            Normalize(data, peak);
            // Short fades at both ends avoid clicks.
            int fade = Mathf.Min(64, data.Length / 2);
            for (int i = 0; i < fade; i++)
            {
                float g = i / (float)fade;
                data[i] *= g;
                data[data.Length - 1 - i] *= g;
            }
            return Create(name, data);
        }

        static AudioClip Loop(string name, float[] data, float peak)
        {
            Normalize(data, peak);
            return Create(name, data);
        }

        static AudioClip Create(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
