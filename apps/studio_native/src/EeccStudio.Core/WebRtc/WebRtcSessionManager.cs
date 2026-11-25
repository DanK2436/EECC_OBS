using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using EeccStudio.Core.Audio;
using EeccStudio.Core.Network;

namespace EeccStudio.Core.WebRtc
{
    /// <summary>
    /// Gestionnaire central des sessions WebRTC de la régie EECC Studio.
    /// Fait le pont entre les caméras smartphones connectées, le serveur de signalisation,
    /// le décodeur vidéo et le mixeur audio professionnel (AudioEngine).
    /// </summary>
    public sealed class WebRtcSessionManager : IDisposable
    {
        private readonly CameraServer _cameraServer;
        private readonly AudioEngine? _audioEngine;
        private readonly ConcurrentDictionary<string, WebRtcCameraReceiver> _receivers = new(StringComparer.OrdinalIgnoreCase);

        public event Action<string, byte[]>? OnVideoFrameReceived;
        public event Action<string, byte[]>? OnAudioFrameReceived;

        public WebRtcSessionManager(CameraServer cameraServer, AudioEngine? audioEngine = null)
        {
            _cameraServer = cameraServer;
            _audioEngine = audioEngine;

            BindServerEvents();
        }

        private void BindServerEvents()
        {
            _cameraServer.OnWebRtcOfferReceived += OnOfferReceived;
            _cameraServer.OnCameraDisconnected += OnCameraDisconnected;
        }

        private void UnbindServerEvents()
        {
            _cameraServer.OnWebRtcOfferReceived -= OnOfferReceived;
            _cameraServer.OnCameraDisconnected -= OnCameraDisconnected;
        }

        private async void OnOfferReceived(CameraClient client, string sdp, string codec, int fps, string resolution)
        {
            var receiver = GetOrCreateReceiver(client);
            await receiver.ProcessOfferAsync(sdp, codec, fps, resolution);
        }

        private void OnCameraDisconnected(string streamName)
        {
            if (_receivers.TryRemove(streamName, out var receiver))
            {
                receiver.Dispose();
            }
        }

        public WebRtcCameraReceiver GetOrCreateReceiver(CameraClient client)
        {
            return _receivers.GetOrAdd(client.StreamName, _ =>
            {
                var receiver = new WebRtcCameraReceiver(client, _cameraServer, _audioEngine);
                receiver.OnVideoFrameAvailable += (slot, frame) =>
                {
                    OnVideoFrameReceived?.Invoke(slot, frame);
                };
                receiver.OnAudioFrameAvailable += (slot, audioData) =>
                {
                    OnAudioFrameReceived?.Invoke(slot, audioData);
                };
                return receiver;
            });
        }

        public WebRtcCameraReceiver? GetReceiver(string streamName)
        {
            _receivers.TryGetValue(streamName, out var receiver);
            return receiver;
        }

        public IReadOnlyCollection<WebRtcCameraReceiver> ActiveReceivers => _receivers.Values;

        public void Dispose()
        {
            UnbindServerEvents();
            foreach (var receiver in _receivers.Values)
            {
                receiver.Dispose();
            }
            _receivers.Clear();
        }
    }
}
