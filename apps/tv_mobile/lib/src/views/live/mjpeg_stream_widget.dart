import 'dart:async';
import 'dart:io';
import 'dart:typed_data';
import 'package:flutter/material.dart';
import '../../constants/colors.dart';

/// Lecteur vidéo réel pour flux MJPEG issu d'EECC Studio (LAN Wi-Fi)
/// Permet d'afficher la vidéo directe de la régie sans plantage de codec ExoPlayer
class MjpegStreamWidget extends StatefulWidget {
  final String streamUrl;
  final BoxFit fit;

  const MjpegStreamWidget({
    super.key,
    required this.streamUrl,
    this.fit = BoxFit.contain,
  });

  @override
  State<MjpegStreamWidget> createState() => _MjpegStreamWidgetState();
}

class _MjpegStreamWidgetState extends State<MjpegStreamWidget> {
  Uint8List? _currentFrameBytes;
  HttpClient? _httpClient;
  HttpClientRequest? _httpRequest;
  StreamSubscription? _streamSubscription;
  Timer? _fallbackPollingTimer;
  bool _isReceivingVideo = false;
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _connectToStream();
  }

  @override
  void didUpdateWidget(covariant MjpegStreamWidget oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.streamUrl != widget.streamUrl) {
      _disconnect();
      _connectToStream();
    }
  }

  @override
  void dispose() {
    _disconnect();
    super.dispose();
  }

  void _disconnect() {
    _fallbackPollingTimer?.cancel();
    _streamSubscription?.cancel();
    try {
      _httpRequest?.abort();
    } catch (_) {}
    _httpClient?.close(force: true);
    _isReceivingVideo = false;
  }

  Future<void> _connectToStream() async {
    setState(() {
      _errorMessage = null;
    });

    try {
      _httpClient = HttpClient()..connectionTimeout = const Duration(seconds: 4);
      final uri = Uri.parse(widget.streamUrl);
      _httpRequest = await _httpClient!.getUrl(uri);
      final response = await _httpRequest!.close();

      if (response.statusCode == 200) {
        final contentType = response.headers.contentType?.value ?? '';
        if (contentType.contains('multipart') || contentType.contains('image')) {
          _readMultipartStream(response);
          return;
        }
      }
    } catch (e) {
      // Échec de la connexion directe
    }

    // Si le stream multipart échoue, basculer sur le polling régulier /live/frame.jpg
    _startPollingFallback();
  }

  void _readMultipartStream(HttpClientResponse response) {
    final buffer = BytesBuilder(copy: false);

    _streamSubscription = response.listen(
      (chunk) {
        buffer.add(chunk);
        final bytes = buffer.asBytes();

        // Recherche du marqueur de fin JPEG (0xFF, 0xD9)
        int eoi = -1;
        for (int i = 0; i < bytes.length - 1; i++) {
          if (bytes[i] == 0xFF && bytes[i + 1] == 0xD9) {
            eoi = i + 2;
            break;
          }
        }

        if (eoi != -1) {
          // Recherche du marqueur de début JPEG (0xFF, 0xD8)
          int soi = -1;
          for (int i = 0; i < eoi - 1; i++) {
            if (bytes[i] == 0xFF && bytes[i + 1] == 0xD8) {
              soi = i;
              break;
            }
          }

          if (soi != -1) {
            final frame = Uint8List.fromList(bytes.sublist(soi, eoi));
            if (mounted) {
              setState(() {
                _currentFrameBytes = frame;
                _isReceivingVideo = true;
                _errorMessage = null;
              });
            }
          }

          final remaining = bytes.sublist(eoi);
          buffer.clear();
          buffer.add(remaining);
        }
      },
      onError: (_) => _startPollingFallback(),
      onDone: () => _startPollingFallback(),
      cancelOnError: true,
    );
  }

  void _startPollingFallback() {
    _fallbackPollingTimer?.cancel();
    final fallbackUrl = widget.streamUrl.replaceAll('/stream', '/frame.jpg');

    _fallbackPollingTimer = Timer.periodic(const Duration(milliseconds: 100), (_) async {
      try {
        final client = HttpClient()..connectionTimeout = const Duration(seconds: 2);
        final req = await client.getUrl(Uri.parse(fallbackUrl));
        final res = await req.close();
        if (res.statusCode == 200) {
          final bytes = await res.fold<List<int>>([], (b, d) => b..addAll(d));
          if (mounted && bytes.isNotEmpty) {
            setState(() {
              _currentFrameBytes = Uint8List.fromList(bytes);
              _isReceivingVideo = true;
              _errorMessage = null;
            });
          }
        } else {
          if (mounted && !_isReceivingVideo) {
            setState(() {
              _errorMessage = 'Régie en attente de direct';
            });
          }
        }
      } catch (_) {
        if (mounted && !_isReceivingVideo) {
          setState(() {
            _errorMessage = 'Régie hors ligne — Vérifiez que EECC Studio est allumé et diffuse sur le réseau';
          });
        }
      }
    });
  }

  @override
  Widget build(BuildContext context) {
    if (_isReceivingVideo && _currentFrameBytes != null) {
      return Image.memory(
        _currentFrameBytes!,
        gaplessPlayback: true,
        fit: widget.fit,
        width: double.infinity,
        height: double.infinity,
      );
    }

    return Container(
      color: Colors.black,
      width: double.infinity,
      height: double.infinity,
      child: Center(
        child: Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: Colors.white.withOpacity(0.08),
                  shape: BoxShape.circle,
                ),
                child: const Icon(Icons.live_tv_rounded, color: Colors.white70, size: 36),
              ),
              const SizedBox(height: 14),
              Text(
                _errorMessage ?? 'Connexion à la régie locale EECC Studio...',
                textAlign: TextAlign.center,
                style: const TextStyle(color: Colors.white70, fontSize: 13),
              ),
              const SizedBox(height: 12),
              OutlinedButton.icon(
                style: OutlinedButton.styleFrom(
                  side: const BorderSide(color: Colors.white24),
                  foregroundColor: Colors.white,
                ),
                icon: const Icon(Icons.refresh_rounded, size: 16),
                label: const Text('Réessayer'),
                onPressed: () {
                  _disconnect();
                  _connectToStream();
                },
              ),
            ],
          ),
        ),
      ),
    );
  }
}
