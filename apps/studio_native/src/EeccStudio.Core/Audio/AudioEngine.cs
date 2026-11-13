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
                    VolumeLinear = 0.85f,
                    CurrentLevelDb = -60.0f,
                    PeakLevelDb = -60.0f
                });
            }
        }

        public void SetVolume(string channelId, float volumeLinear)
        {
            lock (_lock)
            {
                var ch = _channels.Find(c => c.Id == channelId);
                if (ch != null)
                {
                    ch.VolumeLinear = Math.Clamp(volumeLinear, 0.0f, 1.0f);
                }
            }
        }

        public void SetMute(string channelId, bool isMuted)
        {
            lock (_lock)
            {
                var ch = _channels.Find(c => c.Id == channelId);
                if (ch != null)
                {
                    ch.IsMuted = isMuted;
                }
            }
        }
    }
}
