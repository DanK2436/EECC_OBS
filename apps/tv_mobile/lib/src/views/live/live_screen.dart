import 'package:flutter/material.dart';
import 'package:video_player/video_player.dart';
import 'package:chewie/chewie.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../constants/colors.dart';
import '../../models/event_model.dart';
import '../../models/replay_model.dart';
import '../../services/supabase_service.dart';

/// Écran Culte en Direct et Replays 100% FONCTIONNEL
/// Intègre un vrai moteur de lecture vidéo streaming (VideoPlayer + Chewie), zéro simulation.
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

  // Contrôleurs du lecteur vidéo réel
  VideoPlayerController? _videoPlayerController;
  ChewieController? _chewieController;
  bool _isVideoPlayerInitialized = false;
  String? _videoErrorMessage;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _loadDataAndStream();
  }

  @override
  void dispose() {
    _tabController.dispose();
    _chewieController?.dispose();
    _videoPlayerController?.dispose();
    super.dispose();
  }

  Future<void> _loadDataAndStream() async {
    try {
      final snapshot = await _dataService.loadCatalog();
      final event = snapshot.event;

      if (mounted) {
        setState(() {
          _currentEvent = event;
          _replays = snapshot.replays;
          _isLoading = false;
          if (snapshot.error != null) _videoErrorMessage = snapshot.error;
        });

        if (event != null && event.isLive && event.liveStreamUrl != null && event.liveStreamUrl!.isNotEmpty) {
          _initLiveVideoPlayer(event.liveStreamUrl!);
        } else if (event == null && snapshot.error == null) {
          setState(() => _videoErrorMessage = 'Aucun culte en direct ni culte à venir.');
        } else if (event != null && !event.isLive) {
          setState(() => _videoErrorMessage = 'Ce culte n\'est pas en direct. Aucun flux n\'est lancé.');
        } else if (event != null && event.isLive) {
          setState(() => _videoErrorMessage = 'Le culte est marqué en direct, mais aucune adresse de flux n\'est enregistrée.');
        }
      }
    } catch (_) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  /// Initialise le lecteur vidéo matériel réel (HLS .m3u8 ou MP4 streaming)
  Future<void> _initLiveVideoPlayer(String streamUrl) async {
    try {
      final uri = Uri.parse(streamUrl);
      _videoPlayerController = VideoPlayerController.networkUrl(uri);
      await _videoPlayerController!.initialize();

      _chewieController = ChewieController(
        videoPlayerController: _videoPlayerController!,
        autoPlay: true,
        looping: false,
        aspectRatio: 16 / 9,
        allowFullScreen: true,
        allowMuting: true,
        showControls: true,
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
          _videoErrorMessage = 'Le lecteur n\'a pas pu ouvrir ce flux.';
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
            content: Text('Impossible d\'ouvrir le lien vers ${replay.platform}.'),
            backgroundColor: EeccColors.warningOrange,
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: EeccColors.background,
      appBar: AppBar(
        backgroundColor: EeccColors.surface,
        elevation: 0,
        title: const Text(
          'Cultes & Diffusions',
          style: TextStyle(fontWeight: FontWeight.bold, color: EeccColors.textMain),
        ),
        bottom: TabBar(
          controller: _tabController,
          labelColor: EeccColors.primary,
          unselectedLabelColor: EeccColors.textSecondary,
          indicatorColor: EeccColors.primary,
          indicatorWeight: 3,
          tabs: const [
            Tab(text: 'Direct en cours'),
            Tab(text: 'Catalogue des Replays'),
          ],
        ),
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator(color: EeccColors.primary))
          : TabBarView(
              controller: _tabController,
              children: [
                _buildLivePlayerTab(),
                _buildReplaysCatalogTab(),
              ],
            ),
    );
  }

  /// Onglet 1 : Vrai Lecteur Vidéo Direct In-App
  Widget _buildLivePlayerTab() {
    final event = _currentEvent;
    final isLive = event?.isLive ?? false;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // 1. ZONE LECTEUR VIDÉO RÉEL (Chewie / VideoPlayer)
          Container(
            height: 220,
            width: double.infinity,
            decoration: BoxDecoration(
              color: Colors.black,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: EeccColors.border, width: 1),
            ),
            clipBehavior: Clip.antiAlias,
            child: _isVideoPlayerInitialized && _chewieController != null
                ? Chewie(controller: _chewieController!)
                : Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(
                          isLive ? Icons.live_tv_rounded : Icons.schedule_rounded,
                          size: 56,
                          color: isLive ? EeccColors.liveRed : Colors.white54,
                        ),
                        const SizedBox(height: 10),
                        Padding(
                          padding: const EdgeInsets.symmetric(horizontal: 20),
                          child: Text(
                            _videoErrorMessage ?? (isLive ? 'Ouverture du flux enregistré...' : 'Aucun flux à lire.'),
                            textAlign: TextAlign.center,
                            style: const TextStyle(color: Colors.white, fontSize: 13, fontWeight: FontWeight.bold),
                          ),
                        ),
                      ],
                    ),
                  ),
          ),

          const SizedBox(height: 16),

          // 2. BADGE STATUT DIRECT SOLIDE ET TITRE
          Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: event == null ? EeccColors.slate : isLive ? EeccColors.liveRed : EeccColors.primary,
                  borderRadius: BorderRadius.circular(6),
                ),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    CircleAvatar(
                      radius: 4,
                      backgroundColor: Colors.white,
                    ),
                    const SizedBox(width: 6),
                    Text(
                      event == null ? 'AUCUN CULTE' : isLive ? 'EN DIRECT' : 'PROGRAMMÉ',
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 11,
                        fontWeight: FontWeight.w900,
                        letterSpacing: 1,
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(width: 12),
              Text(
                event?.dateFormatted ?? 'Date non enregistrée',
                style: const TextStyle(color: EeccColors.textSecondary, fontSize: 13, fontWeight: FontWeight.w600),
              ),
            ],
          ),

          const SizedBox(height: 12),

          Text(
            event?.title ?? 'Aucun culte enregistré',
            style: const TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.bold,
              color: EeccColors.textMain,
            ),
          ),

          const SizedBox(height: 8),

          Text(
            event?.description ?? 'Aucune description enregistrée.',
            style: const TextStyle(color: EeccColors.textSecondary, fontSize: 14, height: 1.5),
          ),

          const SizedBox(height: 24),

          // 3. CARTE PRÉDICATEUR ET PASSAGE BIBLIQUE
          Container(
            padding: const EdgeInsets.all(16),
            decoration: BoxDecoration(
              color: EeccColors.surface,
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: EeccColors.border),
            ),
            child: Row(
              children: [
                CircleAvatar(
                  radius: 22,
                  backgroundColor: EeccColors.primary,
                  child: const Icon(Icons.person, color: Colors.white),
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        event?.speaker ?? 'Intervenant non indiqué',
                        style: const TextStyle(fontWeight: FontWeight.bold, color: EeccColors.textMain, fontSize: 15),
                      ),
                      Text(
                        event?.bibleReference ?? 'Référence biblique non enregistrée',
                        style: const TextStyle(color: EeccColors.primary, fontSize: 13, fontWeight: FontWeight.w600),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  /// Onglet 2 : Catalogue des Replays
  Widget _buildReplaysCatalogTab() {
    if (_replays.isEmpty) {
      return const Center(
        child: Text(
          'Aucun replay disponible actuellement.',
          style: TextStyle(color: EeccColors.textSecondary),
        ),
      );
    }

    return ListView.builder(
      padding: const EdgeInsets.all(16),
      itemCount: _replays.length,
      itemBuilder: (context, index) {
        final replay = _replays[index];
        return Card(
          margin: const EdgeInsets.only(bottom: 14),
          color: EeccColors.surface,
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
            side: const BorderSide(color: EeccColors.border),
          ),
          child: ListTile(
            contentPadding: const EdgeInsets.all(12),
            leading: ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: Container(
                width: 72,
                height: 52,
                color: EeccColors.primaryDark,
                child: const Icon(Icons.play_arrow_rounded, color: Colors.white, size: 32),
              ),
            ),
            title: Text(
              replay.title,
              style: const TextStyle(fontWeight: FontWeight.bold, color: EeccColors.textMain, fontSize: 14),
            ),
            subtitle: Text(
              '${replay.platform} • ${replay.date?.toLocal().toString().split('.').first ?? 'date non indiquée'} • ${replay.availabilityStatus}',
              style: const TextStyle(color: EeccColors.textSecondary, fontSize: 12),
            ),
            trailing: const Icon(Icons.open_in_new, color: EeccColors.primary, size: 20),
            onTap: () => _openPlatformReplay(replay),
          ),
        );
      },
    );
  }
}
