import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import '../../constants/colors.dart';
import '../../constants/config.dart';

String resolveMediaUrl(String raw) {
  final value = raw.trim();
  if (value.isEmpty) return '';
  if (value.startsWith('http://') || value.startsWith('https://')) return value;
  return '${EeccConfig.supabaseUrl}/storage/v1/object/public/${value.replaceFirst(RegExp(r'^/+'), '')}';
}

class PublishedImage extends StatelessWidget {
  const PublishedImage({
    super.key,
    required this.url,
    this.fit = BoxFit.cover,
  });

  final String url;
  final BoxFit fit;

  @override
  Widget build(BuildContext context) {
    final resolved = resolveMediaUrl(url);
    if (resolved.isEmpty) {
      return const _ImageFallback(message: 'Image absente');
    }
    return Image.network(
      resolved,
      fit: fit,
      gaplessPlayback: true,
      loadingBuilder: (context, child, progress) {
        if (progress == null) return child;
        return const Center(child: CircularProgressIndicator(color: EeccColors.primary));
      },
      errorBuilder: (_, __, ___) => const _ImageFallback(message: 'Image indisponible'),
    );
  }
}

class _ImageFallback extends StatelessWidget {
  const _ImageFallback({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: const Color(0xFF1C2128),
      child: Center(
        child: Text(message, style: const TextStyle(color: Colors.white70, fontSize: 12)),
      ),
    );
  }
}

Future<void> openImageViewer(BuildContext context, String url, String title, {bool vertical = false}) {
  return Navigator.of(context).push(
    MaterialPageRoute(
      builder: (_) => ImageViewerPage(url: url, title: title, vertical: vertical),
    ),
  );
}

class ImageViewerPage extends StatefulWidget {
  const ImageViewerPage({
    super.key,
    required this.url,
    required this.title,
    required this.vertical,
  });

  final String url;
  final String title;
  final bool vertical;

  @override
  State<ImageViewerPage> createState() => _ImageViewerPageState();
}

class _ImageViewerPageState extends State<ImageViewerPage> {
  late bool _vertical = widget.vertical;

  @override
  void initState() {
    super.initState();
    _applyOrientation();
  }

  @override
  void dispose() {
    SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
    super.dispose();
  }

  void _applyOrientation() {
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.immersiveSticky);
    SystemChrome.setPreferredOrientations(
      _vertical
          ? [DeviceOrientation.portraitUp]
          : [DeviceOrientation.landscapeLeft, DeviceOrientation.landscapeRight],
    );
  }

  void _setVertical(bool vertical) {
    setState(() => _vertical = vertical);
    _applyOrientation();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      body: Stack(
        children: [
          Center(child: PublishedImage(url: widget.url, fit: BoxFit.contain)),
          Positioned(
            left: 12,
            right: 12,
            bottom: 16,
            child: SafeArea(
              child: Row(
                children: [
                  IconButton(
                    onPressed: () => Navigator.pop(context),
                    icon: const Icon(Icons.close, color: Colors.white),
                  ),
                  Expanded(
                    child: Text(
                      widget.title,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600),
                    ),
                  ),
                  _layoutButton(Icons.stay_current_portrait, 'Vertical', _vertical, () => _setVertical(true)),
                  _layoutButton(Icons.stay_current_landscape, 'Horizontal', !_vertical, () => _setVertical(false)),
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _layoutButton(IconData icon, String label, bool selected, VoidCallback onTap) {
    return TextButton.icon(
      onPressed: onTap,
      icon: Icon(icon, color: selected ? EeccColors.primary : Colors.white),
      label: Text(label, style: TextStyle(color: selected ? EeccColors.primary : Colors.white)),
    );
  }
}
