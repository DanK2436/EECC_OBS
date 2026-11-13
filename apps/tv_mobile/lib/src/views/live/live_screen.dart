import 'dart:async';
import 'package:flutter/material.dart';
import 'package:video_player/video_player.dart';
import 'package:chewie/chewie.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../constants/colors.dart';
import '../../models/event_model.dart';
import '../../models/replay_model.dart';
import '../../services/supabase_service.dart';
import 'mjpeg_stream_widget.dart';

/// Écran Culte en Direct et Replays 100% FONCTIONNEL
/// Reçoit en direct les flux d'EECC Studio (LAN Wi-Fi et Cloud)
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

  // Configuration de liaison avec la régie EECC Studio
  String _studioIp = '192.168.1.100';
  int _studioPort = 8089;
  bool _isStudioConnected = false;

  // Contrôleurs du lecteur vidéo réel
  VideoPlayerController? _videoPlayerController;
  ChewieController? _chewieController;
  bool _isVideoPlayerInitialized = false;
  String? _videoErrorMessage;
  Timer? _liveRefreshTimer;

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _loadDataAndStream();
    _liveRefreshTimer = Timer.periodic(const Duration(seconds: 4), (_) {
      if (mounted) _pollStudioStatus();
    });
  }

  @override
  void dispose() {
    _liveRefreshTimer?.cancel();
    _tabController.dispose();
    _chewieController?.dispose();
    _videoPlayerController?.dispose();
    super.dispose();
  }

  Future<void> _pollStudioStatus() async {
    try {
      final event = await _dataService.getCurrentOrNextEvent(customStudioIp: _studioIp, port: _studioPort);
      if (mounted) {
        if (_currentEvent?.status != event.status || _isStudioConnected != (event.id == 'live-studio-lan')) {
          setState(() {
            _currentEvent = event;
            _isStudioConnected = event.id == 'live-studio-lan';
          });
        }
      }
    } catch (_) {}
  }

  Future<void> _loadDataAndStream() async {
    setState(() => _isLoading = true);
    try {
      final event = await _dataService.getCurrentOrNextEvent(customStudioIp: _studioIp, port: _studioPort);
      final replays = await _dataService.getReplaysCatalog();

      if (mounted) {
        setState(() {
          _currentEvent = event;
          _replays = replays;
          _isLoading = false;
          _isStudioConnected = event.id == 'live-studio-lan';
        });

        if (event.liveStreamUrl != null && event.liveStreamUrl!.isNotEmpty) {
          _initLiveVideoPlayer(event.liveStreamUrl!);
        }
      }
    } catch (_) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  /// Initialise le lecteur vidéo matériel réel (HLS .m3u8 ou flux de régie)
  Future<void> _initLiveVideoPlayer(String streamUrl) async {
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
          _videoErrorMessage = 'Flux en direct détecté. Synchronisation avec la régie EECC Studio en cours...';
        });
      }
    }
  }

  void _showStudioConnectionDialog() {
    final ipController = TextEditingController(text: _studioIp);
    final portController = TextEditingController(text: _studioPort.toString());

    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: EeccColors.surface,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: Row(
          children: [
            const Icon(Icons.router_rounded, color: EeccColors.primary),
            const SizedBox(width: 8),
            const Text('Connexion Régie EECC Studio', style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
          ],
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'Entrez l\'adresse IP du PC exécutant EECC Studio sur le réseau Wi-Fi local de l\'église :',
              style: TextStyle(fontSize: 13, color: EeccColors.textSecondary),
            ),
            const SizedBox(height: 14),
            TextField(
              controller: ipController,
              decoration: const InputDecoration(
                labelText: 'Adresse IP du Studio',
                hintText: '192.168.1.100',
                border: OutlineInputBorder(),
                prefixIcon: Icon(Icons.wifi_rounded),
              ),
              keyboardType: TextInputType.datetime,
            ),
            const SizedBox(height: 12),
            TextField(
              controller: portController,
              decoration: const InputDecoration(
                labelText: 'Port Broadcast',
                hintText: '8089',
                border: OutlineInputBorder(),
                prefixIcon: Icon(Icons.numbers_rounded),
              ),
              keyboardType: TextInputType.number,
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Annuler', style: TextStyle(color: EeccColors.textSecondary)),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(
              backgroundColor: EeccColors.primary,
              foregroundColor: Colors.white,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
            ),
            onPressed: () {
              Navigator.pop(ctx);
              setState(() {
                _studioIp = ipController.text.trim();
                _studioPort = int.tryParse(portController.text.trim()) ?? 8089;
              });
              _loadDataAndStream();
            },
            child: const Text('Connecter au Direct'),
          ),
        ],
      ),
    );
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
        title: Row(
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: Image.asset('assets/images/logo_official.png', height: 28, width: 28, fit: BoxFit.contain),
            ),
            const SizedBox(width: 8),
            const Text(
              'EECC TV',
              style: TextStyle(fontWeight: FontWeight.w900, color: EeccColors.textMain, fontSize: 18),
            ),
          ],
        ),
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: 8),
            child: TextButton.icon(
              style: TextButton.styleFrom(
                backgroundColor: _isStudioConnected ? EeccColors.successGreen.withOpacity(0.15) : EeccColors.primary.withOpacity(0.1),
                foregroundColor: _isStudioConnected ? EeccColors.successGreen : EeccColors.primary,
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
              ),
              icon: Icon(_isStudioConnected ? Icons.wifi_rounded : Icons.wifi_find_rounded, size: 16),
              label: Text(_isStudioConnected ? 'Régie Connectée' : 'Régie Locale', style: const TextStyle(fontSize: 12, fontWeight: FontWeight.bold)),
              onPressed: _showStudioConnectionDialog,
            ),
          ),
        ],
        bottom: TabBar(
          controller: _tabController,
          labelColor: EeccColors.primary,
          unselectedLabelColor: EeccColors.textSecondary,
          indicatorColor: EeccColors.primary,
          indicatorWeight: 3,
          tabs: const [
            Tab(text: 'Direct Culte'),
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
    final isLive = event?.isLive ?? true;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // 1. ZONE LECTEUR VIDÉO RÉEL (Chewie / VideoPlayer / Écran de régie)
          Container(
            height: 220,
            width: double.infinity,
            decoration: BoxDecoration(
              color: Colors.black,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(color: isLive ? EeccColors.liveRed : EeccColors.border, width: isLive ? 2 : 1),
              boxShadow: [
                BoxShadow(
                  color: (isLive ? EeccColors.liveRed : Colors.black).withOpacity(0.15),
                  blurRadius: 12,
                  offset: const Offset(0, 4),
                ),
              ],
            ),
            clipBehavior: Clip.antiAlias,
            child: _buildVideoArea(event, isLive),
          ),

          const SizedBox(height: 16),

          // 2. BADGE STATUT DIRECT SOLIDE ET TITRE
          Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: isLive ? EeccColors.liveRed : EeccColors.primary,
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
                      isLive ? 'EN DIRECT' : 'PROGRAMMÉ',
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
              const SizedBox(width: 10),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                decoration: BoxDecoration(
                  color: EeccColors.surface,
                  borderRadius: BorderRadius.circular(4),
                  border: Border.all(color: EeccColors.border),
                ),
                child: Text(
                  _isStudioConnected ? 'Régie Studio Locale' : 'Diffusion Web',
                  style: TextStyle(
                    color: _isStudioConnected ? EeccColors.successGreen : EeccColors.textSecondary,
                    fontSize: 11,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
            ],
          ),

          const SizedBox(height: 12),

          Text(
            event?.title ?? 'Grand Culte d\'Adoration et d\'Enseignement',
            style: const TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.bold,
              color: EeccColors.textMain,
            ),
          ),

          const SizedBox(height: 6),

          const Text(
            'Église Évangélique les Cohéritiers du Christ (EECC)',
            style: TextStyle(color: EeccColors.primary, fontSize: 13, fontWeight: FontWeight.bold),
          ),

          const SizedBox(height: 8),

          Text(
            event?.description ?? 'Rejoignez la communauté EECC pour un temps puissant dans la présence de Dieu et l\'enseignement de la Parole.',
            style: const TextStyle(color: EeccColors.textSecondary, fontSize: 14, height: 1.5),
          ),

          const SizedBox(height: 20),

          // 3. CARTE PRÉDICATEUR ET PROGRAMME
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
                        event?.speaker ?? 'Pasteur Principal',
                        style: const TextStyle(fontWeight: FontWeight.bold, color: EeccColors.textMain, fontSize: 15),
                      ),
                      Text(
                        event?.theme ?? 'La Puissance de la Foi Agissante',
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
              '${replay.platform} • ${replay.speaker}',
              style: const TextStyle(color: EeccColors.textSecondary, fontSize: 12),
            ),
            trailing: const Icon(Icons.open_in_new, color: EeccColors.primary, size: 20),
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
                  'DIFFUSION EN ATTENTE DU CULTE',
                  style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 13, letterSpacing: 0.5),
                ),
                const SizedBox(height: 6),
                const Text(
                  'Le culte n\'a pas encore démarré sur la régie EECC Studio.',
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

    final url = event?.liveStreamUrl ?? 'http://$_studioIp:$_studioPort/live/stream';
    if (url.contains(':8089') || url.endsWith('/stream') || url.endsWith('.jpg')) {
      return MjpegStreamWidget(
        streamUrl: url,
        fit: BoxFit.contain,
      );
    }

    if (_isVideoPlayerInitialized && _chewieController != null) {
      return Chewie(controller: _chewieController!);
    }

    return MjpegStreamWidget(
      streamUrl: 'http://$_studioIp:$_studioPort/live/stream',
      fit: BoxFit.contain,
    );
  }
}
