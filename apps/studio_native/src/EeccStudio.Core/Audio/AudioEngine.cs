using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace EeccStudio.Core.Audio
{
    public class AudioChannel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public float VolumeLinear { get; set; } = 1.0f; // 0.0 à 1.0
        public bool IsMuted { get; set; }
        public float CurrentLevelDb { get; set; } = -60.0f; // -inf à 0 dB
        public float PeakLevelDb { get; set; } = -60.0f;
    }

    /// <summary>
    /// Moteur Audio Broadcast WASAPI / Multi-Pistes 32-bit float
    /// Gère les faders de mixage et fournit les niveaux réels pour les vu-mètres à paliers solides.
    /// </summary>
    public class AudioEngine
    {
        private readonly List<AudioChannel> _channels = new();
        private readonly ProgramAudioMix _mix = new();
        private readonly object _lock = new();

        public IReadOnlyList<AudioChannel> Channels
        {
            get
            {
                lock (_lock) return _channels.ToArray();
            }
        }

        public AudioEngine()
        {
            // Initialisation des canaux audio broadcast standard pour le culte
            AddChannel("master", "Programme Général (Master)");
            AddChannel("pastor_mic", "Micro Pupitre / Prédicateur");
            AddChannel("choir_music", "Chœur & Orchestre (Stéréo)");
            AddChannel("mobile_cam_1", "Audio Caméra Mobile 1");
            AddChannel("media_playback", "Vidéos & Diaporamas");
        }

        public void AddChannel(string id, string name)
        {
            lock (_lock)
            {
                _channels.Add(new AudioChannel
                {
                    Id = id,
                    Name = name,
                    VolumeLinear = id == "master" ? 0.65f : 0.85f,
                    CurrentLevelDb = -60.0f,
                    PeakLevelDb = -60.0f
                });
                _mix.SetGain(id, id == "master" ? 0.65f : 0.85f);
            }
        }

        public void SetVolume(string channelId, float volumeLinear)
        {
            var volume = Math.Clamp(volumeLinear, 0.0f, 1.0f);
            _mix.SetGain(channelId, volume);
            lock (_lock)
            {
                var ch = _channels.Find(c => c.Id == channelId);
                if (ch != null)
                    ch.VolumeLinear = volume;
            }
        }

        public void SetMute(string channelId, bool isMuted)
        {
            _mix.SetMute(channelId, isMuted);
            lock (_lock)
            {
                var ch = _channels.Find(c => c.Id == channelId);
                if (ch != null)
                    ch.IsMuted = isMuted;
            }
        }

        /// <summary>Ajoute des échantillons stéréo entrelacés sur une piste du bus.</summary>
        public void SubmitPcm(string channelId, ReadOnlySpan<short> interleavedStereo)
        {
            _mix.Submit(channelId, interleavedStereo);
        }

        /// <summary>Lit le bus master pour une destination. Le résultat suit les échantillons réellement soumis.</summary>
        public byte[] RenderMaster(int frames)
        {
            var bytes = _mix.Render(frames);
            lock (_lock)
            {
                var master = _channels.Find(channel => channel.Id == "master");
                if (master != null)
                {
                    var db = (float)_mix.MasterLevelDb;
                    master.CurrentLevelDb = db;
                    if (db > master.PeakLevelDb)
                        master.PeakLevelDb = db;
                }
            }

            return bytes;
        }
    }
}
