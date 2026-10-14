using System.Collections.Generic;
using UnityEngine;

namespace LastHearth.Game
{
    /// <summary>
    /// Music, the crackle of the hearth and the short sounds of the game (Resources/Audio). Quiet by
    /// default and easy to switch off: music and sounds each have their own toggle, kept between runs.
    /// </summary>
    public sealed class AudioDirector
    {
        const string MusicKey = "lasthearth.music";
        const string SoundKey = "lasthearth.sound";

        readonly AudioSource music;
        readonly AudioSource ambience;
        readonly AudioSource sfx;
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        string playing = "";

        public bool MusicOn { get; private set; }
        public bool SoundOn { get; private set; }

        public AudioDirector(GameObject host)
        {
            music = host.AddComponent<AudioSource>();
            ambience = host.AddComponent<AudioSource>();
            sfx = host.AddComponent<AudioSource>();
            music.loop = ambience.loop = true;
            music.playOnAwake = ambience.playOnAwake = sfx.playOnAwake = false;
            MusicOn = PlayerPrefs.GetInt(MusicKey, 1) == 1;
            SoundOn = PlayerPrefs.GetInt(SoundKey, 1) == 1;
        }

        AudioClip Clip(string name)
        {
            if (!clips.TryGetValue(name, out var c))
                clips[name] = c = UnityEngine.Resources.Load<AudioClip>("Audio/" + name);
            return c;
        }

        /// <summary>Called every frame: battle music in fights and the Surge, the hearth theme otherwise.</summary>
        public void Tick(bool fighting, bool nearHearth)
        {
            string want = fighting ? "Battle" : "Hearth";
            if (want != playing)
            {
                playing = want;
                music.clip = Clip(want);
                if (music.clip != null)
                    music.Play();
            }
            music.volume = MusicOn ? (fighting ? 0.32f : 0.24f) : 0f;
            if (ambience.clip == null)
            {
                ambience.clip = Clip("Fire");
                if (ambience.clip != null)
                    ambience.Play();
            }
            ambience.volume = SoundOn && nearHearth && !fighting ? 0.18f : 0f;
        }

        public void Play(string name, float volume = 0.6f)
        {
            if (!SoundOn)
                return;
            var c = Clip(name);
            if (c != null)
                sfx.PlayOneShot(c, volume);
        }

        public void ToggleMusic()
        {
            MusicOn = !MusicOn;
            PlayerPrefs.SetInt(MusicKey, MusicOn ? 1 : 0);
        }

        public void ToggleSound()
        {
            SoundOn = !SoundOn;
            PlayerPrefs.SetInt(SoundKey, SoundOn ? 1 : 0);
        }
    }
}
