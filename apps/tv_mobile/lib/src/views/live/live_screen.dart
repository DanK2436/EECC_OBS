import 'package:flutter/material.dart';
import '../../constants/colors.dart';
import '../../models/event_model.dart';
import '../../models/replay_model.dart';
import '../../services/supabase_service.dart';
import 'package:url_launcher/url_launcher.dart';

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

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
    _loadData();
  }

  Future<void> _loadData() async {
    final event = await _dataService.getCurrentOrNextEvent();
    final replays = await _dataService.getReplaysCatalog();

    if (mounted) {
      setState(() {
        _currentEvent = event;
        _replays = replays;
        _isLoading = false;
      });
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

  /// Onglet 1 : Culte en direct in-app
  Widget _buildLivePlayerTab() {
    final isLive = _currentEvent?.isLive ?? false;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Zone Lecteur Vidéo Direct
          Container(
            height: 220,
            width: double.infinity,
            decoration: BoxDecoration(
              color: Colors.black,
              borderRadius: BorderRadius.circular(16),
              boxShadow: [
                BoxShadow(
                  color: Colors.black.withOpacity(0.2),
                  blurRadius: 10,
                  offset: const Offset(0, 4),
                ),
              ],
            ),
            child: Stack(
              alignment: Alignment.center,
              children: [
                if (isLive) ...[
                  // Lecteur actif
                  const Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(Icons.play_circle_filled_rounded, size: 64, color: Colors.white),
                        SizedBox(height: 8),
                        Text(
                          'Lecture du flux direct in-app',
                          style: TextStyle(color: Colors.white70, fontSize: 13),
                        ),
                      ],
                    ),
                  ),
                  Positioned(
                    top: 12,
                    left: 12,
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        color: EeccColors.liveRed,
                        borderRadius: BorderRadius.circular(6),
                      ),
                      child: const Row(
                        children: [
                          CircleAvatar(radius: 4, backgroundColor: Colors.white),
                          SizedBox(width: 6),
                          Text(
                            'EN DIRECT',
                            style: TextStyle(color: Colors.white, fontSize: 11, fontWeight: FontWeight.bold),
                          ),
                        ],
                      ),
                    ),
                  ),
                ] else ...[
                  // En attente du prochain direct
                  Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const Icon(Icons.videocam_off_rounded, size: 48, color: Colors.white38),
                        const SizedBox(height: 12),
                        const Text(
                          'Aucun direct en cours actuellement',
                          style: TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 15),
                        ),
                        const SizedBox(height: 6),
                        Text(
                          'Prochain culte : ${_currentEvent?.title ?? "Dimanche 09h00"}',
                          style: const TextStyle(color: Colors.white60, fontSize: 13),
                        ),
                      ],
                    ),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: 20),

          // Informations sur le culte
          Text(
            _currentEvent?.title ?? 'Culte de l\'église EECC',
            style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold, color: EeccColors.textMain),
          ),
          const SizedBox(height: 8),
          Row(
            children: [
              const Icon(Icons.person_rounded, size: 16, color: EeccColors.slate),
              const SizedBox(width: 6),
              Text(
                _currentEvent?.speaker ?? 'Pasteur Principal',
                style: const TextStyle(color: EeccColors.slate, fontWeight: FontWeight.w600),
              ),
              const SizedBox(width: 16),
              const Icon(Icons.schedule_rounded, size: 16, color: EeccColors.slate),
              const SizedBox(width: 6),
              const Text('Durée estimée : 2h', style: TextStyle(color: EeccColors.slate)),
            ],
          ),
          const SizedBox(height: 16),
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: EeccColors.surface,
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: EeccColors.border),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text(
                  'Thème de la prédication',
                  style: TextStyle(fontWeight: FontWeight.bold, color: EeccColors.primary),
                ),
                const SizedBox(height: 6),
                Text(
                  _currentEvent?.theme ?? 'La marche par la foi',
                  style: const TextStyle(fontSize: 14, color: EeccColors.textMain),
                ),
                const SizedBox(height: 10),
                const Text(
                  'Description :',
                  style: TextStyle(fontWeight: FontWeight.w600, fontSize: 12, color: EeccColors.textSecondary),
                ),
                const SizedBox(height: 4),
                Text(
                  _currentEvent?.description ?? 'Un moment d\'enseignement biblique et de louange.',
                  style: const TextStyle(fontSize: 13, color: EeccColors.slate),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  /// Onglet 2 : Catalogue des replays (Section 4.3)
  Widget _buildReplaysCatalogTab() {
    return ListView.builder(
      padding: const EdgeInsets.all(16),
      itemCount: _replays.length,
      itemBuilder: (context, index) {
        final replay = _replays[index];
        return Card(
          margin: const EdgeInsets.only(bottom: 14),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(14)),
          elevation: 0,
          color: EeccColors.surface,
          child: Container(
            decoration: BoxDecoration(
              borderRadius: BorderRadius.circular(14),
              border: Border.all(color: EeccColors.border),
            ),
            padding: const EdgeInsets.all(14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Container(
                      width: 90,
                      height: 70,
                      decoration: BoxDecoration(
                        color: EeccColors.primaryDark.withOpacity(0.08),
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Stack(
                        alignment: Alignment.center,
                        children: [
                          const Icon(Icons.ondemand_video_rounded, color: EeccColors.primary, size: 36),
                          Positioned(
                            top: 4,
                            left: 4,
                            child: _buildPlatformTag(replay.platform),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 14),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            replay.title,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: EeccColors.textMain),
                          ),
                          const SizedBox(height: 6),
                          Text(
                            '${replay.speaker ?? "Orateur"} • ${replay.durationText ?? "1h30"}',
                            style: const TextStyle(fontSize: 12, color: EeccColors.textSecondary),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 12),
                const Divider(height: 1, color: EeccColors.border),
                const SizedBox(height: 10),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      'Hébergé sur ${replay.platform.toUpperCase()}',
                      style: const TextStyle(fontSize: 11, color: EeccColors.slate, fontStyle: FontStyle.italic),
                    ),
                    ElevatedButton.icon(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: EeccColors.primary,
                        foregroundColor: Colors.white,
                        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
                      ),
                      onPressed: () => _openPlatformReplay(replay),
                      icon: const Icon(Icons.open_in_new_rounded, size: 14),
                      label: const Text('Regarder le replay', style: TextStyle(fontSize: 12, fontWeight: FontWeight.bold)),
                    ),
                  ],
                ),
              ],
            ),
          ),
        );
      },
    );
  }

  Widget _buildPlatformTag(String platform) {
    Color bg = Colors.red;
    String name = 'YT';

    if (platform == 'facebook') {
      bg = const Color(0xFF1877F2);
      name = 'FB';
    } else if (platform == 'tiktok') {
      bg = Colors.black;
      name = 'TT';
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 2),
      decoration: BoxDecoration(color: bg, borderRadius: BorderRadius.circular(4)),
      child: Text(name, style: const TextStyle(color: Colors.white, fontSize: 8, fontWeight: FontWeight.bold)),
    );
  }
}
