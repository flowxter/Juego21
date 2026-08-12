using System;
using UnityEngine;

namespace Blackjack.Client.UI
{
    /// <summary>
    /// Sonido de la mesa, sintetizado por código.
    ///
    /// No hay ficheros de audio en el proyecto: los clips se generan muestra a
    /// muestra al arrancar. Así no hacen falta descargas ni licencias, y el
    /// resultado pesa cero en el repositorio.
    ///
    /// El sonido es la mitad de la sensación de una mesa. Sin el clic de las
    /// fichas y el roce de las cartas, la misma escena parece una maqueta.
    /// </summary>
    public sealed class CasinoAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        private static CasinoAudio _instance;

        private AudioSource _effects;
        private AudioSource _music;

        private AudioClip _chip;
        private AudioClip _deal;
        private AudioClip _flip;
        private AudioClip _win;
        private AudioClip _lose;
        private AudioClip _blackjack;
        private AudioClip _click;
        private AudioClip _shuffle;

        public static CasinoAudio Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var go = new GameObject("[CasinoAudio]");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<CasinoAudio>();
                return _instance;
            }
        }

        public bool MusicEnabled
        {
            get => _music != null && !_music.mute;
            set { if (_music != null) _music.mute = !value; }
        }

        private void Awake()
        {
            _instance = this;

            _effects = gameObject.AddComponent<AudioSource>();
            _effects.playOnAwake = false;
            _effects.volume = 0.55f;

            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.volume = 0.16f;

            BuildClips();

            _music.clip = BuildLounge();
            _music.Play();
        }

        private void BuildClips()
        {
            // Ficha: golpe seco de arcilla. Un transitorio de ruido y un tono
            // medio que muere enseguida; alargarlo lo convierte en "plástico".
            _chip = Render("chip", 0.13f, (t, d) =>
            {
                float env = Mathf.Exp(-t * 46f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 130f) * 0.6f;
                float body = Mathf.Sin(2f * Mathf.PI * 780f * t) * 0.5f;
                return (noise + body) * env;
            });

            // Reparto: la carta rozando el tapete. Ruido filtrado que sube y
            // baja, sin tono definido.
            _deal = Render("deal", 0.20f, (t, d) =>
            {
                float progress = t / d;
                float env = Mathf.Sin(progress * Mathf.PI);
                float noise = UnityEngine.Random.value * 2f - 1f;
                // Filtrado pobre pero suficiente: se atenúa el agudo con el tiempo.
                return noise * env * 0.32f * Mathf.Lerp(1f, 0.35f, progress);
            });

            _flip = Render("flip", 0.10f, (t, d) =>
            {
                float env = Mathf.Exp(-t * 55f);
                float noise = UnityEngine.Random.value * 2f - 1f;
                return noise * env * 0.30f;
            });

            _click = Render("click", 0.06f, (t, d) =>
            {
                float env = Mathf.Exp(-t * 90f);
                return Mathf.Sin(2f * Mathf.PI * 1350f * t) * env * 0.28f;
            });

            // Barajado: ráfaga larga de roces sucesivos.
            _shuffle = Render("shuffle", 0.85f, (t, d) =>
            {
                float progress = t / d;
                float env = Mathf.Sin(progress * Mathf.PI);
                float flutter = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 17f * t);
                float noise = UnityEngine.Random.value * 2f - 1f;
                return noise * env * flutter * 0.22f;
            });

            // Premio: arpegio mayor ascendente, brillante y corto.
            float[] winNotes = { 523.25f, 659.25f, 783.99f, 1046.50f };
            _win = Render("win", 0.62f, (t, d) =>
            {
                int step = Mathf.Min(winNotes.Length - 1, (int)(t / 0.13f));
                float local = t - step * 0.13f;
                float env = Mathf.Exp(-local * 8f);
                return Chime(winNotes[step], t) * env * 0.34f;
            });

            // Derrota: dos tonos que caen. Breve, sin ensañarse.
            _lose = Render("lose", 0.42f, (t, d) =>
            {
                float freq = t < 0.16f ? 320f : 240f;
                float local = t < 0.16f ? t : t - 0.16f;
                float env = Mathf.Exp(-local * 7f);
                return Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.26f;
            });

            // Blackjack: fanfarria de cinco notas.
            float[] fanfare = { 523.25f, 659.25f, 783.99f, 1046.50f, 1318.51f };
            _blackjack = Render("blackjack", 0.95f, (t, d) =>
            {
                int step = Mathf.Min(fanfare.Length - 1, (int)(t / 0.11f));
                float local = t - step * 0.11f;
                float env = Mathf.Exp(-local * 5.5f);
                float shimmer = Chime(fanfare[step], t) + 0.3f * Chime(fanfare[step] * 2f, t);
                return shimmer * env * 0.30f;
            });
        }

        /// <summary>
        /// Bucle de fondo: progresión ii-V-I suave, tipo salón.
        ///
        /// Volumen bajo y ataques lentos a propósito. La música de una mesa
        /// tiene que poder ignorarse: si se nota, molesta a la décima ronda.
        /// </summary>
        private static AudioClip BuildLounge()
        {
            // Dm7 · G7 · Cmaj7 · Am7
            float[][] chords =
            {
                new[] { 146.83f, 174.61f, 220.00f, 261.63f },
                new[] { 196.00f, 246.94f, 293.66f, 349.23f },
                new[] { 130.81f, 164.81f, 196.00f, 246.94f },
                new[] { 110.00f, 130.81f, 164.81f, 196.00f }
            };

            const float barLength = 2.6f;
            float duration = chords.Length * barLength;
            int samples = (int)(duration * SampleRate);
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)SampleRate;
                int bar = Mathf.Min(chords.Length - 1, (int)(t / barLength));
                float local = t - bar * barLength;

                // Entrada y salida suaves en cada acorde.
                float env = Mathf.Min(1f, local / 0.5f) * Mathf.Min(1f, (barLength - local) / 0.7f);

                float value = 0f;
                foreach (float freq in chords[bar])
                {
                    // Ligero desafine entre voces: un acorde perfectamente
                    // afinado suena sintético.
                    value += Mathf.Sin(2f * Mathf.PI * freq * t) * 0.25f;
                    value += Mathf.Sin(2f * Mathf.PI * freq * 1.003f * t) * 0.12f;
                }

                data[i] = value * env * 0.22f;
            }

            // Fundido en los extremos para que el bucle no chasquee al cerrar.
            int fade = SampleRate / 4;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] *= k;
                data[samples - 1 - i] *= k;
            }

            AudioClip clip = AudioClip.Create("lounge", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Tono con armónico, más rico que un seno pelado.</summary>
        private static float Chime(float frequency, float t)
            => Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.7f
             + Mathf.Sin(2f * Mathf.PI * frequency * 2f * t) * 0.2f;

        private static AudioClip Render(string name, float duration, Func<float, float, float> generator)
        {
            int samples = Mathf.Max(1, (int)(duration * SampleRate));
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                data[i] = Mathf.Clamp(generator(i / (float)SampleRate, duration), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ------------------------------------------------------------------

        private void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0.06f)
        {
            if (clip == null) return;

            // Variar levemente el tono evita el efecto "ametralladora" cuando
            // suena el mismo golpe varias veces seguidas.
            _effects.pitch = 1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter);
            _effects.PlayOneShot(clip, volume);
        }

        public void Chip() => Play(_chip, 0.9f);

        public void Deal() => Play(_deal, 0.8f, 0.10f);

        public void Flip() => Play(_flip, 0.9f);

        public void Click() => Play(_click, 0.7f);

        public void Shuffle() => Play(_shuffle, 0.7f, 0.02f);

        public void Win() => Play(_win, 0.9f, 0.02f);

        public void Lose() => Play(_lose, 0.8f, 0.02f);

        public void Blackjack() => Play(_blackjack, 1f, 0f);
    }
}
