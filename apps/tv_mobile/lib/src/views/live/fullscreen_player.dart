import 'dart:async';
import 'package:chewie/chewie.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:video_player/video_player.dart';
import '../../constants/colors.dart';

Future<void> openProgramFullscreen(
  BuildContext context, {
  String? videoUrl,
  String? frameUrl,
  required bool vertical,
}) {
  return Navigator.of(context).push(
    MaterialPageRoute(
      builder: (_) => ProgramFullscreenPage(
        videoUrl: videoUrl,
        frameUrl: frameUrl,
        vertical: vertical,
      ),
    ),
  );
}

class ProgramFullscreenPage extends StatefulWidget {
  const ProgramFullscreenPage({
    super.key,
    this.videoUrl,
    this.frameUrl,
    required this.vertical,
  });

  final String? videoUrl;
  final String? frameUrl;
  final bool vertical;

  @override
  State<ProgramFullscreenPage> createState() => _ProgramFullscreenPageState();
}

class _ProgramFullscreenPageState extends State<ProgramFullscreenPage> {
  late bool _vertical = widget.vertical;
  VideoPlayerController? _video;
  ChewieController? _chewie;
  String? _error;

  @override
  void initState() {
    super.initState();
    _applyOrientation();
    final url = widget.videoUrl;
    if (url != null && url.isNotEmpty) {
      _openVideo(url);
    }
  }

  @override
  void dispose() {
    _chewie?.dispose();
    _video?.dispose();
    SystemChrome.setPreferredOrientations([DeviceOrientation.portraitUp]);
    SystemChrome.setEnabledSystemUIMode(SystemUiMode.edgeToEdge);
    super.dispose();
  }

  Future<void> _openVideo(String url) async {
    final video = VideoPlayerController.networkUrl(Uri.parse(url));
    _video = video;
    try {
      await video.initialize();
      if (!mounted) return;
      final ratio = video.value.aspectRatio == 0 ? 16 / 9 : video.value.aspectRatio;
      _chewie = ChewieController(
        videoPlayerController: video,
        autoPlay: true,
        looping: false,
        aspectRatio: ratio,
        allowFullScreen: false,
        allowMuting: true,
        allowPlaybackSpeedChanging: true,
        showControls: true,
        materialProgressColors: ChewieProgressColors(
          playedColor: EeccColors.liveRed,
          handleColor: EeccColors.liveRed,
          backgroundColor: EeccColors.border,
          bufferedColor: EeccColors.slate,
        ),
      );
      setState(() => _error = null);
    } catch (_) {
      if (mounted) setState(() => _error = 'Cette vidéo ne peut pas être lue.');
    }
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
    final frame = widget.frameUrl;
    Widget picture;
    if (frame != null && frame.isNotEmpty) {
      picture = _LiveFrame(url: frame);
    } else if (_error != null) {
      picture = Center(child: Text(_error!, style: const TextStyle(color: Colors.white)));
    } else if (_chewie != null) {
      picture = Chewie(controller: _chewie!);
    } else {
      picture = const Center(child: CircularProgressIndicator(color: EeccColors.liveRed));
    }

    return Scaffold(
      backgroundColor: Colors.black,
      body: Stack(
        children: [
          Positioned.fill(child: picture),
          Positioned(
            left: 8,
            right: 8,
            top: 8,
            child: SafeArea(
              child: Row(
                children: [
                  IconButton(
                    onPressed: () => Navigator.pop(context),
                    icon: const Icon(Icons.close, color: Colors.white),
                  ),
                  const Spacer(),
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
      label: Text(label, style: TextStyle(color: selected ? EeccColors.primary : Colors.white, fontSize: 12)),
    );
  }
}

class _LiveFrame extends StatefulWidget {
  const _LiveFrame({required this.url});

  final String url;

  @override
  State<_LiveFrame> createState() => _LiveFrameState();
}

class _LiveFrameState extends State<_LiveFrame> {
  Timer? _timer;
  int _tick = 0;

  @override
  void initState() {
    super.initState();
    _timer = Timer.periodic(const Duration(milliseconds: 200), (_) {
      if (mounted) setState(() => _tick++);
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final separator = widget.url.contains('?') ? '&' : '?';
    return ColoredBox(
      color: Colors.black,
      child: Image.network(
        '${widget.url}${separator}t=$_tick',
        gaplessPlayback: true,
        fit: BoxFit.contain,
        errorBuilder: (_, __, ___) => const Center(
          child: Text('Aucune image du direct.', style: TextStyle(color: Colors.white70)),
        ),
      ),
    );
  }
}
