import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:crypto/crypto.dart';
import 'package:flutter_webrtc/flutter_webrtc.dart';

import '../pairing/pairing_ticket.dart';

class LanMediaLink {
  LanMediaLink(this.ticket);

  final PairingTicket ticket;
  final RTCVideoRenderer localRenderer = RTCVideoRenderer();
  MediaStream? localStream;
  RTCPeerConnection? peer;
  WebSocket? _socket;
  HttpClient? _http;
  Timer? _batteryTimer;
  final List<Map<String, dynamic>> _pendingIce = [];
  bool _remoteSet = false;
  bool _closed = false;
  bool _accepted = false;
  bool _rendererReady = false;

  void Function(String status)? onStatus;
  void Function(String? tally)? onTally;
  void Function(int? battery)? onBattery;

  Future<void> openCapture() async {
    await localRenderer.initialize();
    _rendererReady = true;
    try {
      localStream = await navigator.mediaDevices.getUserMedia({
        'audio': {
          'echoCancellation': true,
          'noiseSuppression': true,
        },
        'video': {
          'facingMode': 'environment',
          'width': {'ideal': 640},
          'height': {'ideal': 480},
          'frameRate': {'ideal': 15, 'max': 15},
        },
      });
    } catch (error) {
      onStatus?.call('Caméra ou micro refusé. ${_safe(error)}');
      rethrow;
    }
    localRenderer.srcObject = localStream;
    onStatus?.call('Caméra et micro ouverts. Aucune image n\'est encore envoyée.');
  }

  Future<void> connect({Future<int?> Function()? readBattery}) async {
    if (_closed) return;
    _batteryTimer?.cancel();
    if (ticket.expiresAt.isBefore(DateTime.now().toUtc())) {
      onStatus?.call('QR expiré avant l\'envoi. Générez-en un nouveau dans Studio.');
      return;
    }
    final client = HttpClient()..connectionTimeout = const Duration(seconds: 8);
    client.badCertificateCallback = (cert, host, port) {
      final actual = sha256.convert(cert.der).toString();
      return actual == ticket.fingerprint;
    };
    _http = client;
    try {
      _socket = await WebSocket.connect(
        'wss://${ticket.ip}:${ticket.port}/pair',
        customClient: client,
      );
    } catch (error) {
      onStatus?.call('Connexion TLS refusée. ${_safe(error)}');
      return;
    }
    _socket!.listen(_onSocketData, onError: (error) {
      onStatus?.call('Liaison interrompue. ${_safe(error)}');
    }, onDone: () {
      if (!_closed) onStatus?.call('Studio a fermé la liaison.');
    });
    _send({
      'type': 'hello',
      'v': PairingTicket.protocolVersion,
      'token': ticket.token,
    });
    onStatus?.call('Jeton envoyé, attente de la validation Studio.');
    if (readBattery != null) {
      Future<void> publishBattery() async {
        try {
          final level = await readBattery();
          if (level != null && level >= 0 && level <= 100) {
            _send({'type': 'telemetry', 'battery': level});
            onBattery?.call(level);
          } else {
            onBattery?.call(null);
          }
        } catch (_) {
          onBattery?.call(null);
        }
      }

      await publishBattery();
      _batteryTimer = Timer.periodic(const Duration(seconds: 5), (_) => publishBattery());
    }
  }

  Future<void> setMicMuted(bool muted) async {
    final tracks = localStream?.getAudioTracks() ?? [];
    for (final track in tracks) {
      track.enabled = !muted;
      await Helper.setMicrophoneMute(muted, track);
    }
    _send({'type': 'audio', 'muted': muted});
    onStatus?.call(muted ? 'Micro coupé sur le téléphone.' : 'Micro ouvert sur le téléphone.');
  }

  Future<bool> setTorch(bool enabled) async {
    final track = _videoTrack;
    if (track == null) {
      onStatus?.call('Torche indisponible : aucune piste vidéo.');
      return false;
    }
    try {
      if (!await track.hasTorch()) {
        onStatus?.call('Cet appareil ne signale pas de torche.');
        return false;
      }
      await track.setTorch(enabled);
      return true;
    } catch (error) {
      onStatus?.call('Torche indisponible sur cet appareil. ${_safe(error)}');
      return false;
    }
  }

  Future<void> switchCamera() async {
    final track = _videoTrack;
    if (track == null) return;
    try {
      await Helper.switchCamera(track);
    } catch (error) {
      onStatus?.call('Changement de caméra impossible. ${_safe(error)}');
    }
  }

  Future<void> setZoom(double zoom) async {
    final track = _videoTrack;
    if (track == null) return;
    try {
      await Helper.setZoom(track, zoom);
    } catch (error) {
      onStatus?.call('Zoom indisponible. ${_safe(error)}');
    }
  }

  Future<void> reconnectMedia() async {
    if (_socket == null || _socket!.readyState != WebSocket.open) {
      onStatus?.call('La session est fermée. Un jeton déjà utilisé sera refusé : générez un nouveau QR.');
      await connect();
      return;
    }
    await _sendOffer();
  }

  Future<void> close() async {
    _closed = true;
    _batteryTimer?.cancel();
    try {
      _send({'type': 'bye'});
    } catch (_) {}
    await _closeMedia();
    try {
      await _socket?.close();
    } catch (_) {}
    _http?.close(force: true);
  }

  MediaStreamTrack? get _videoTrack {
    final tracks = localStream?.getVideoTracks() ?? [];
    return tracks.isEmpty ? null : tracks.first;
  }

  Future<void> _onSocketData(dynamic data) async {
    if (data is! String) return;
    Object? decoded;
    try {
      decoded = jsonDecode(data);
    } catch (_) {
      onStatus?.call('Message Studio illisible.');
      return;
    }
    if (decoded is! Map) return;
    final type = decoded['type'];
    if (type == 'hello-ok') {
      _accepted = true;
      onStatus?.call('Jeton accepté. Envoi de l\'offre audio/vidéo.');
      await _sendOffer();
    } else if (type == 'hello-rejected') {
      final reason = decoded['reason'];
      onStatus?.call(_rejection(reason));
      onTally?.call(null);
    } else if (type == 'answer') {
      final sdp = decoded['sdp'];
      if (sdp is! String || peer == null) {
        onStatus?.call('Réponse média vide.');
        return;
      }
      await peer!.setRemoteDescription(RTCSessionDescription(sdp, 'answer'));
      _remoteSet = true;
      for (final ice in _pendingIce) {
        await _addIce(ice);
      }
      _pendingIce.clear();
      onStatus?.call('Réponse média reçue.');
    } else if (type == 'ice') {
      if (!_remoteSet) {
        _pendingIce.add(Map<String, dynamic>.from(decoded));
      } else {
        await _addIce(Map<String, dynamic>.from(decoded));
      }
    } else if (type == 'tally') {
      final state = decoded['state'];
      if (state == 'preview' || state == 'program' || state == 'standby') {
        onTally?.call(state as String);
      }
    } else if (type == 'renegotiate' && _accepted) {
      await _sendOffer();
    }
  }

  Future<void> _sendOffer() async {
    final stream = localStream;
    if (stream == null) {
      onStatus?.call('Offre impossible : caméra non ouverte.');
      return;
    }
    await peer?.close();
    _remoteSet = false;
    peer = await createPeerConnection({
      'iceServers': <dynamic>[],
      'sdpSemantics': 'unified-plan',
    });
    for (final track in stream.getTracks()) {
      await peer!.addTrack(track, stream);
    }
    peer!.onIceCandidate = (candidate) {
      if (candidate.candidate == null) return;
      _send({
        'type': 'ice',
        'candidate': candidate.candidate,
        'sdpMid': candidate.sdpMid,
        'sdpMLineIndex': candidate.sdpMLineIndex,
      });
    };
    peer!.onIceConnectionState = (state) {
      onStatus?.call('ICE $state');
    };
    final offer = await peer!.createOffer();
    final sdp = offer.sdp ?? '';
    if (!sdp.contains('VP8') || !sdp.contains('PCMU')) {
      onStatus?.call('Offre sans VP8 ou PCMU. Studio ne peut pas la décoder.');
    }
    await peer!.setLocalDescription(offer);
    _send({'type': 'offer', 'sdp': sdp});
  }

  Future<void> _addIce(Map<String, dynamic> message) async {
    final candidate = message['candidate'];
    if (candidate is! String || peer == null) return;
    try {
      await peer!.addCandidate(RTCIceCandidate(
        candidate,
        message['sdpMid'] as String?,
        message['sdpMLineIndex'] is int ? message['sdpMLineIndex'] as int : null,
      ));
    } catch (error) {
      onStatus?.call('Candidat ICE refusé. ${_safe(error)}');
    }
  }

  void _send(Map<String, dynamic> message) {
    final socket = _socket;
    if (socket == null || socket.readyState != WebSocket.open) return;
    socket.add(jsonEncode(message));
  }

  Future<void> _closeMedia() async {
    await peer?.close();
    peer = null;
    for (final track in localStream?.getTracks() ?? <MediaStreamTrack>[]) {
      await track.stop();
    }
    await localStream?.dispose();
    localStream = null;
    if (_rendererReady) {
      _rendererReady = false;
      await localRenderer.dispose();
    }
  }

  String _rejection(Object? reason) {
    switch (reason) {
      case 'expired':
        return 'Jeton expiré. Générez un nouveau QR dans Studio.';
      case 'used':
        return 'Jeton déjà utilisé. Générez un nouveau QR dans Studio.';
      case 'version':
        return 'Version de QR non reconnue.';
      default:
        return 'Jeton refusé.';
    }
  }

  String _safe(Object error) {
    var text = error.toString();
    if (text.contains(ticket.token)) text = text.replaceAll(ticket.token, '[jeton]');
    return text;
  }
}
