import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:video_player/video_player.dart';
import 'package:chewie/chewie.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../constants/colors.dart';
import 'fullscreen_player.dart';
import '../../models/event_model.dart';
import '../../models/replay_model.dart';
import '../../services/supabase_service.dart';

/// Écran Culte en Direct et Replays
/// Reçoit exclusivement ses flux et programmations depuis la base cloud Supabase
class LiveScreen extends StatefulWidget {
  const LiveScreen({super.key});

  @override
  State<LiveScreen> createState() => _LiveScreenState();
}

class _LiveScreenState extends State<LiveScreen> with SingleTickerProviderStateMixin {
  late TabController _tabController;
  final EeccDataService _dataService = EeccDataService();

  ChurchEvent? _currentEvent;
  List<ReplayItem> _replays = [];
  bool _isLoading = true;

  // Contrôleurs du lecteur vidéo réel HLS / MP4
  VideoPlayerController? _videoPlayerController;
  ChewieController? _chewieController;
  bool _isVideoPlayerInitialized = false;
  String? _videoErrorMessage;
  String? _studioFrameUrl;
  Timer? _liveRefreshTimer;
  void Function()? _stopCaption;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _loadDataAndStream();
    _stopCaption = _dataService.watchLiveCaption(_applyCaption);
    _liveRefreshTimer = Timer.periodic(const Duration(seconds: 2), (_) {
      if (mounted) _pollSupabaseStatus();
    });
  }

  @override
  void dispose() {
    _stopCaption?.call();
    _liveRefreshTimer?.cancel();
    _tabController.dispose();
    _chewieController?.dispose();
    _videoPlayerController?.dispose();
    super.dispose();
  }

  Future<void> _pollSupabaseStatus() async {
    try {
      final event = await _dataService.getCurrentOrNextEvent();
      if (mounted) {
        if (_currentEvent?.status != event.status ||
            _currentEvent?.liveStreamUrl != event.liveStreamUrl ||
            _currentEvent?.title != event.title ||
            _currentEvent?.speaker != event.speaker ||
            _currentEvent?.theme != event.theme) {
          setState(() {
            _currentEvent = event;
          });
          if (event.status == 'live' && event.liveStreamUrl != null && event.liveStreamUrl!.isNotEmpty) {
            _initLiveVideoPlayer(event.liveStreamUrl!);
          }
        }
      }
    } catch (_) {}
  }

  void _applyCaption(Map<String, dynamic> row) {
    if (!mounted) return;
    final title = (row['title'] as String?) ?? '';
    final speaker = row['speaker'] as String?;
    final theme = row['theme'] as String?;
    final current = _currentEvent;
    setState(() {
      _currentEvent = ChurchEvent(
        id: current?.id ?? 'studio-live',
        title: title.isNotEmpty ? title : (current?.title ?? 'Culte'),
        description: current?.description,
        startTime: current?.startTime ?? DateTime.now(),
        status: current?.status ?? 'scheduled',
        liveStreamUrl: current?.liveStreamUrl,
        speaker: (speaker != null && speaker.isNotEmpty) ? speaker : current?.speaker,
        theme: (theme != null && theme.isNotEmpty) ? theme : current?.theme,
      );
    });
  }

  Future<void> _loadDataAndStream() async {
    setState(() => _isLoading = true);
    try {
      final event = await _dataService.getCurrentOrNextEvent();
      final replays = await _dataService.getReplaysCatalog();

      if (mounted) {
        setState(() {
          _currentEvent = event;
          _replays = replays;
          _isLoading = false;
        });

        if (event.status == 'live' && event.liveStreamUrl != null && event.liveStreamUrl!.isNotEmpty) {
          _initLiveVideoPlayer(event.liveStreamUrl!);
        }
      }
    } catch (_) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  /// Initialise le lecteur vidéo matériel réel (HLS .m3u8 ou flux hébergé)
  Future<void> _initLiveVideoPlayer(String streamUrl) async {
    if (_isStudioJpeg(streamUrl)) {
      _chewieController?.dispose();
      _videoPlayerController?.dispose();
      _chewieController = null;
      _videoPlayerController = null;
      if (mounted) {
        setState(() {
          _studioFrameUrl = _studioFrameEndpoint(streamUrl);
          _isVideoPlayerInitialized = false;
          _videoErrorMessage = null;
        });
      }
      return;
    }

    _studioFrameUrl = null;
    try {
      _chewieController?.dispose();
      _videoPlayerController?.dispose();
      _isVideoPlayerInitialized = false;

      final uri = Uri.parse(streamUrl);
      _videoPlayerController = VideoPlayerController.networkUrl(uri);
      await _videoPlayerController!.initialize();

      _chewieController = ChewieController(
        videoPlayerController: _videoPlayerController!,
        autoPlay: true,
        looping: true,
        aspectRatio: _videoPlayerController!.value.aspectRatio == 0
            ? 16 / 9
            : _videoPlayerController!.value.aspectRatio,
        allowFullScreen: true,
        allowMuting: true,
        allowPlaybackSpeedChanging: true,
        showControls: true,
        deviceOrientationsOnEnterFullScreen: const [
          DeviceOrientation.landscapeLeft,
          DeviceOrientation.landscapeRight,
        ],
        deviceOrientationsAfterFullScreen: const [
          DeviceOrientation.portraitUp,
        ],
        materialProgressColors: ChewieProgressColors(
          playedColor: EeccColors.liveRed,
          handleColor: EeccColors.liveRed,
          backgroundColor: EeccColors.border,
          bufferedColor: EeccColors.slate,
        ),
      );

      if (mounted) {
        setState(() {
          _isVideoPlayerInitialized = true;
          _videoErrorMessage = null;
        });
      }
    } catch (e) {
      if (mounted) {
        setState(() {
          _isVideoPlayerInitialized = false;
          _videoErrorMessage = 'Flux en direct non disponible pour le moment.';
        });
      }
    }
  }

  Future<void> _openPlatformReplay(ReplayItem replay) async {
    final uri = Uri.parse(replay.url);
    if (await canLaunchUrl(uri)) {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
    } else {
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Impossible d\'ouvrir le lien : ${replay.url}'),
            backgroundColor: EeccColors.danger,
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final isLive = _currentEvent?.status == 'live';

    return Scaffold(
      backgroundColor: EeccColors.background,
      appBar: AppBar(
        title: Row(
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(6),
              child: Image.asset('assets/images/logo_official.png', width: 28, height: 28),
            ),
            const SizedBox(width: 10),
            const Text(
              'EECC TV Direct',
              style: TextStyle(fontWeight: FontWeight.bold, fontSize: 17),
            ),
          ],
        ),
        actions: [
          IconButton(
            tooltip: 'Actualiser depuis Supabase',
            icon: const Icon(Icons.refresh_rounded, color: EeccColors.textSecondary),
            onPressed: _loadDataAndStream,
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator(color: EeccColors.primary))
          : Column(
              children: [
                // 1. ZONE DE LECTURE VIDÉO OFFICIELLE
                AspectRatio(
                  aspectRatio: 16 / 9,
                  child: Stack(
                    children: [
                      Positioned.fill(child: _buildVideoArea(_currentEvent, isLive)),
                      Positioned(
                        right: 6,
                        bottom: 6,
                        child: Row(
                          children: [
                            _fullButton(Icons.stay_current_landscape, false),
                            const SizedBox(width: 6),
                            _fullButton(Icons.stay_current_portrait, true),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),

                // 2. BANDEAU DE STATUT OFFICIEL
                Container(
                  color: EeccColors.surface,
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                  child: Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: isLive ? EeccColors.liveRed : EeccColors.badgeScheduled,
                          borderRadius: BorderRadius.circular(6),
                        ),
                        child: Row(
                          children: [
                            if (isLive) ...[
                              Container(
                                width: 8,
                                height: 8,
                                decoration: const BoxDecoration(
                                  color: Colors.white,
                                  shape: BoxShape.circle,
                                ),
                              ),
                              const SizedBox(width: 6),
                            ],
                            Text(
                              isLive ? 'EN DIRECT' : 'PROGRAMMÉ',
                              style: TextStyle(
                                color: isLive ? Colors.white : EeccColors.textSecondary,
                                fontWeight: FontWeight.bold,
                                fontSize: 11,
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(
                          _currentEvent?.title ?? 'Culte dominical EECC',
                          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                        decoration: BoxDecoration(
                          color: EeccColors.primary.withOpacity(0.1),
                          borderRadius: BorderRadius.circular(4),
                        ),
                        child: const Text(
                          'Cloud Supabase',
                          style: TextStyle(fontSize: 10, color: EeccColors.primary, fontWeight: FontWeight.bold),
                        ),
                      ),
                    ],
                  ),
                ),

                // 3. ONGLET DE NAVIGATION (Infos Culte / Replays)
                TabBar(
                  controller: _tabController,
                  indicatorColor: EeccColors.primary,
                  labelColor: EeccColors.primary,
                  unselectedLabelColor: EeccColors.textSecondary,
                  tabs: const [
                    Tab(text: 'Détails du Culte'),
                    Tab(text: 'Replays Disponibles'),
                  ],
                ),

                // 4. CONTENU DE L'ONGLET
                Expanded(
                  child: TabBarView(
                    controller: _tabController,
                    children: [
                      _buildEventDetailsTab(_currentEvent),
                      _buildReplaysTab(),
                    ],
                  ),
                ),
              ],
            ),
    );
  }

  Widget _buildEventDetailsTab(ChurchEvent? event) {
    if (event == null) return const SizedBox();

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Text(
          event.title,
          style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold, color: EeccColors.textMain),
        ),
        const SizedBox(height: 8),
        Row(
          children: [
            const Icon(Icons.person_rounded, size: 16, color: EeccColors.primary),
            const SizedBox(width: 6),
            Text(
              event.speaker ?? 'Pasteur Principal',
              style: const TextStyle(fontWeight: FontWeight.w600, color: EeccColors.textMain),
            ),
          ],
        ),
        const SizedBox(height: 6),
        Row(
          children: [
            const Icon(Icons.bookmark_rounded, size: 16, color: EeccColors.secondary),
            const SizedBox(width: 6),
            Expanded(
              child: Text(
                'Thème : ${event.theme ?? "Enseignement Biblique"}',
                style: const TextStyle(color: EeccColors.textSecondary, fontSize: 13),
              ),
            ),
          ],
        ),
        const Divider(height: 24),
        const Text(
          'À propos de ce moment :',
          style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
        ),
        const SizedBox(height: 6),
        Text(
          event.description ?? '',
          style: const TextStyle(color: EeccColors.textSecondary, height: 1.4, fontSize: 13),
        ),
      ],
    );
  }

  Widget _buildReplaysTab() {
    if (_replays.isEmpty) {
      return const Center(
        child: Text(
          'Aucun replay disponible pour le moment.',
          style: TextStyle(color: EeccColors.textSecondary),
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: _replays.length,
      separatorBuilder: (_, __) => const SizedBox(height: 10),
      itemBuilder: (context, index) {
        final replay = _replays[index];
        IconData platformIcon = Icons.video_library_rounded;
        Color platformColor = EeccColors.primary;

        if (replay.platform.toLowerCase() == 'youtube') {
          platformIcon = Icons.play_arrow_rounded;
          platformColor = const Color(0xFFFF0000);
        } else if (replay.platform.toLowerCase() == 'facebook') {
          platformIcon = Icons.thumb_up_rounded;
          platformColor = const Color(0xFF1877F2);
        } else if (replay.platform.toLowerCase() == 'tiktok') {
          platformIcon = Icons.music_note_rounded;
          platformColor = Colors.black87;
        }

        return Card(
          elevation: 0,
          color: EeccColors.surface,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(10),
            side: const BorderSide(color: EeccColors.border),
          ),
          child: ListTile(
            leading: CircleAvatar(
              backgroundColor: platformColor.withOpacity(0.12),
              child: Icon(platformIcon, color: platformColor),
            ),
            title: Text(
              replay.title,
              style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14),
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
            ),
            subtitle: Text(
              '${replay.speaker} • ${replay.platform.toUpperCase()}',
              style: const TextStyle(color: EeccColors.textSecondary, fontSize: 12),
            ),
            trailing: const Icon(Icons.open_in_new_rounded, size: 18, color: EeccColors.primary),
            onTap: () => _openPlatformReplay(replay),
          ),
        );
      },
    );
  }

  Widget _buildVideoArea(ChurchEvent? event, bool isLive) {
    if (!isLive) {
      return Container(
        color: const Color(0xFF0D1117),
        child: Center(
          child: Padding(
            padding: const EdgeInsets.all(20),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(10),
                  child: Image.asset('assets/images/logo_official.png', width: 44, height: 44),
                ),
                const SizedBox(height: 12),
                const Text(
                  'DIFFUSION EN ATTENTE DU DIRECT',
                  style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 13, letterSpacing: 0.5),
                ),
                const SizedBox(height: 6),
                const Text(
                  'Le culte officiel n\'a pas encore démarré.',
                  textAlign: TextAlign.center,
                  style: TextStyle(color: EeccColors.textSecondary, fontSize: 12),
                ),
                const SizedBox(height: 12),
                OutlinedButton.icon(
                  style: OutlinedButton.styleFrom(
                    side: const BorderSide(color: Colors.white24),
                    foregroundColor: Colors.white,
                  ),
                  icon: const Icon(Icons.refresh_rounded, size: 16),
                  label: const Text('Actualiser'),
                  onPressed: _loadDataAndStream,
                ),
              ],
            ),
          ),
        ),
      );
    }

    if (_studioFrameUrl != null) {
      return StudioProgramView(frameUrl: _studioFrameUrl!);
    }

    if (_isVideoPlayerInitialized && _chewieController != null) {
      return Chewie(controller: _chewieController!);
    }

    if (event?.liveStreamUrl != null && event!.liveStreamUrl!.isNotEmpty) {
      final streamUrl = event.liveStreamUrl!;
      return Container(
        color: Colors.black,
        child: Center(
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              const CircularProgressIndicator(color: EeccColors.liveRed),
              const SizedBox(height: 12),
              const Text('Connexion au flux en direct Supabase...', style: TextStyle(color: Colors.white, fontSize: 12)),
              const SizedBox(height: 10),
              ElevatedButton.icon(
                style: ElevatedButton.styleFrom(
                  backgroundColor: EeccColors.primary,
                  foregroundColor: Colors.white,
                ),
                icon: const Icon(Icons.open_in_browser_rounded, size: 16),
                label: const Text('Ouvrir le flux officiel'),
                onPressed: () async {
                  final uri = Uri.parse(streamUrl);
                  if (await canLaunchUrl(uri)) {
                    await launchUrl(uri, mode: LaunchMode.externalApplication);
                  }
                },
              ),
            ],
          ),
        ),
      );
    }

    return Container(
      color: Colors.black,
      child: const Center(
        child: Text(
          'En attente du flux en direct...',
          style: TextStyle(color: Colors.white70),
        ),
      ),
    );
  }

  Widget _fullButton(IconData icon, bool vertical) {
    final url = _currentEvent?.liveStreamUrl;
    final ready = url != null && url.isNotEmpty && _currentEvent?.status == 'live';
    return Material(
      color: Colors.black54,
      borderRadius: BorderRadius.circular(8),
      child: IconButton(
        tooltip: vertical ? 'Plein écran vertical' : 'Plein écran horizontal',
        onPressed: ready
            ? () {
                final jpeg = _isStudioJpeg(url);
                openProgramFullscreen(
                  context,
                  videoUrl: jpeg ? null : url,
                  frameUrl: jpeg ? _studioFrameEndpoint(url) : null,
                  vertical: vertical,
                );
              }
            : null,
        icon: Icon(icon, color: Colors.white, size: 20),
      ),
    );
  }

  bool _isStudioJpeg(String url) =>
      url.contains('/live/stream') || url.contains('/live/frame.jpg');

  String _studioFrameEndpoint(String url) {
    final bare = url.split('?').first;
    if (bare.endsWith('/live/frame.jpg')) return bare;
    return bare.replaceFirst('/live/stream', '/live/frame.jpg');
  }
}

class StudioProgramView extends StatefulWidget {
  const StudioProgramView({super.key, required this.frameUrl});

  final String frameUrl;

  @override
  State<StudioProgramView> createState() => _StudioProgramViewState();
}

class _StudioProgramViewState extends State<StudioProgramView> {
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
    final separator = widget.frameUrl.contains('?') ? '&' : '?';
    return ColoredBox(
      color: Colors.black,
      child: Image.network(
        '${widget.frameUrl}${separator}t=$_tick',
        gaplessPlayback: true,
        fit: BoxFit.contain,
        errorBuilder: (_, __, ___) => const Center(
          child: Text(
            'EECC Studio est en direct, mais aucune image n\'arrive encore.',
            textAlign: TextAlign.center,
            style: TextStyle(color: Colors.white70),
          ),
        ),
      ),
    );
  }
}
