import 'package:flutter/material.dart';
import '../../constants/colors.dart';
import '../../models/event_model.dart';
import '../../models/replay_model.dart';
import '../../models/podcast_model.dart';
import '../../services/supabase_service.dart';
import '../donation/donation_modal.dart';
import 'package:url_launcher/url_launcher.dart';

class HomeScreen extends StatefulWidget {
  final Function(int) onNavigateTab;

  const HomeScreen({super.key, required this.onNavigateTab});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  final EeccDataService _dataService = EeccDataService();
  ChurchEvent? _currentEvent;
  List<ReplayItem> _replays = [];
  List<PodcastItem> _podcasts = [];
  String? _error;
  bool _isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadData();
  }

  Future<void> _loadData() async {
    final snapshot = await _dataService.loadCatalog();
    if (mounted) {
      setState(() {
        _currentEvent = snapshot.event;
        _replays = snapshot.replays;
        _podcasts = snapshot.podcasts;
        _error = snapshot.error;
        _isLoading = false;
      });
    }
  }

  Future<void> _launchExternalUrl(String url) async {
    final uri = Uri.parse(url);
    if (await canLaunchUrl(uri)) {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_isLoading) {
      return const Center(child: CircularProgressIndicator(color: EeccColors.primary));
    }

    return Scaffold(
      backgroundColor: EeccColors.background,
      appBar: AppBar(
        backgroundColor: EeccColors.surface,
        elevation: 0,
        title: Row(
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(8),
              child: Image.asset(
                'assets/images/logo.png',
                width: 32,
                height: 32,
                fit: BoxFit.contain,
              ),
            ),
            const SizedBox(width: 10),
            const Text(
              'EECC TV',
              style: TextStyle(
                fontWeight: FontWeight.bold,
                fontSize: 20,
                color: EeccColors.textMain,
              ),
            ),
          ],
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.notifications_none_rounded, color: EeccColors.slate),
            onPressed: () {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('Les notifications ne sont pas branchées.')),
              );
            },
          ),
          IconButton(
            icon: const Icon(Icons.favorite_outline_rounded, color: EeccColors.primary),
            onPressed: () => DonationModal.show(context),
          ),
        ],
      ),
      body: RefreshIndicator(
        onRefresh: _loadData,
        color: EeccColors.primary,
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: Text(_error!, style: const TextStyle(color: EeccColors.warningOrange)),
                ),
              _buildLiveOrScheduledCard(),

              const SizedBox(height: 16),

              // 2. BANNIÈRE SOUTENIR L'ÉGLISE
              _buildDonationCard(),

              const SizedBox(height: 24),

              // 3. RAIL HORIZONTAL : REPLAYS À RETROUVER (YouTube, Facebook, TikTok)
              _buildReplaysRail(),

              const SizedBox(height: 24),

              // 4. RAIL HORIZONTAL : DERNIERS PODCASTS ET PRÉDICATIONS
              _buildPodcastsRail(),

              const SizedBox(height: 30),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildLiveOrScheduledCard() {
    final isLive = _currentEvent?.isLive ?? false;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: isLive
              ? [const Color(0xFF991B1B), const Color(0xFFDC2626)]
              : [EeccColors.primaryDark, EeccColors.primary],
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
        ),
        borderRadius: BorderRadius.circular(16),
        boxShadow: [
          BoxShadow(
            color: (isLive ? EeccColors.liveRed : EeccColors.primary).withOpacity(0.25),
            blurRadius: 10,
            offset: const Offset(0, 4),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: isLive ? Colors.white : Colors.white.withOpacity(0.2),
                  borderRadius: BorderRadius.circular(6),
                ),
                child: Row(
                  children: [
                    CircleAvatar(
                      radius: 4,
                      backgroundColor: isLive ? EeccColors.liveRed : Colors.white,
                    ),
                    const SizedBox(width: 6),
                    Text(
                      isLive ? 'EN DIRECT' : 'PROCHAIN CULTE',
                      style: TextStyle(
                        color: isLive ? EeccColors.liveRed : Colors.white,
                        fontSize: 11,
                        fontWeight: FontWeight.bold,
                        letterSpacing: 0.5,
                      ),
                    ),
                  ],
                ),
              ),
              const Spacer(),
              if (!isLive && _currentEvent != null)
                Text(
                  _currentEvent!.dateFormatted,
                  style: const TextStyle(color: Colors.white70, fontSize: 13),
                ),
            ],
          ),
          const SizedBox(height: 12),
          Text(
            _currentEvent?.title ?? 'Aucun culte programmé',
            style: const TextStyle(
              color: Colors.white,
              fontSize: 18,
              fontWeight: FontWeight.bold,
            ),
          ),
          const SizedBox(height: 6),
          Text(
            _currentEvent?.theme ?? _currentEvent?.description ?? 'Aucune description enregistrée.',
            style: const TextStyle(color: Colors.white70, fontSize: 13),
          ),
          const SizedBox(height: 16),
          ElevatedButton.icon(
            style: ElevatedButton.styleFrom(
              backgroundColor: Colors.white,
              foregroundColor: isLive ? EeccColors.liveRed : EeccColors.primary,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            ),
            onPressed: () => widget.onNavigateTab(1), // Aller sur l'onglet Direct
            icon: Icon(isLive ? Icons.play_arrow_rounded : Icons.calendar_today_rounded, size: 20),
            label: Text(
              isLive ? 'Regarder le direct maintenant' : 'Accéder au direct & replays',
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildDonationCard() {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: EeccColors.surface,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: EeccColors.border),
      ),
      child: Row(
        children: [
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(
              color: EeccColors.primary.withOpacity(0.08),
              borderRadius: BorderRadius.circular(10),
            ),
            child: const Icon(Icons.volunteer_activism_rounded, color: EeccColors.primary),
          ),
          const SizedBox(width: 12),
          const Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Soutenez la mission',
                  style: TextStyle(fontWeight: FontWeight.bold, fontSize: 14, color: EeccColors.textMain),
                ),
                Text(
                  'Le paiement n\'est pas encore branché',
                  style: TextStyle(fontSize: 12, color: EeccColors.textSecondary),
                ),
              ],
            ),
          ),
          TextButton(
            onPressed: () => DonationModal.show(context),
            style: TextButton.styleFrom(
              foregroundColor: EeccColors.primary,
              textStyle: const TextStyle(fontWeight: FontWeight.bold),
            ),
            child: const Text('Donner'),
          ),
        ],
      ),
    );
  }

  Widget _buildReplaysRail() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            const Text(
              'Replays à retrouver',
              style: TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.bold,
                color: EeccColors.textMain,
              ),
            ),
            TextButton(
              onPressed: () => widget.onNavigateTab(1),
              child: const Text('Tout voir', style: TextStyle(color: EeccColors.primary)),
            ),
          ],
        ),
        const SizedBox(height: 10),
        if (_replays.isEmpty)
          const Text('Aucun replay enregistré.', style: TextStyle(color: EeccColors.textSecondary))
        else
        SizedBox(
          height: 190,
          child: ListView.builder(
            scrollDirection: Axis.horizontal,
            itemCount: _replays.length,
            itemBuilder: (context, index) {
              final replay = _replays[index];
              return Container(
                width: 240,
                margin: const EdgeInsets.only(right: 12),
                decoration: BoxDecoration(
                  color: EeccColors.surface,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: EeccColors.border),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Container(
                      height: 100,
                      decoration: BoxDecoration(
                        color: EeccColors.slate.withOpacity(0.1),
                        borderRadius: const BorderRadius.vertical(top: Radius.circular(12)),
                      ),
                      child: Stack(
                        alignment: Alignment.center,
                        children: [
                          const Icon(Icons.video_library_rounded, size: 40, color: EeccColors.slateLight),
                          Positioned(
                            bottom: 8,
                            right: 8,
                            child: Container(
                              padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                              decoration: BoxDecoration(
                                color: Colors.black.withOpacity(0.7),
                                borderRadius: BorderRadius.circular(4),
                              ),
                              child: Text(
                                replay.durationText ?? 'Replay',
                                style: const TextStyle(color: Colors.white, fontSize: 10),
                              ),
                            ),
                          ),
                          Positioned(
                            top: 8,
                            left: 8,
                            child: _buildPlatformBadge(replay.platform),
                          ),
                        ],
                      ),
                    ),
                    Padding(
                      padding: const EdgeInsets.all(10),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            replay.title,
                            maxLines: 2,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 12),
                          ),
                          const SizedBox(height: 6),
                          InkWell(
                            onTap: () => _launchExternalUrl(replay.url),
                            child: Row(
                              children: [
                                const Text(
                                  'Ouvrir sur la plateforme',
                                  style: TextStyle(
                                    fontSize: 11,
                                    fontWeight: FontWeight.w600,
                                    color: EeccColors.primary,
                                  ),
                                ),
                                const SizedBox(width: 4),
                                const Icon(Icons.open_in_new_rounded, size: 12, color: EeccColors.primary),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              );
            },
          ),
        ),
      ],
    );
  }

  Widget _buildPodcastsRail() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            const Text(
              'Podcasts & Prédications',
              style: TextStyle(
                fontSize: 17,
                fontWeight: FontWeight.bold,
                color: EeccColors.textMain,
              ),
            ),
            TextButton(
              onPressed: () => widget.onNavigateTab(2),
              child: const Text('Médiathèque', style: TextStyle(color: EeccColors.primary)),
            ),
          ],
        ),
        const SizedBox(height: 8),
        if (_podcasts.isEmpty)
          const Text('Aucun sermon audio enregistré.', style: TextStyle(color: EeccColors.textSecondary)),
        ..._podcasts.map((podcast) {
          return Container(
            margin: const EdgeInsets.only(bottom: 10),
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: EeccColors.surface,
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: EeccColors.border),
            ),
            child: Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: EeccColors.primary.withOpacity(0.08),
                    borderRadius: BorderRadius.circular(10),
                  ),
                  child: const Icon(Icons.podcasts_rounded, color: EeccColors.primary),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        podcast.title,
                        style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                      Text(
                        '${podcast.speaker} • ${podcast.formattedDuration}',
                        style: const TextStyle(fontSize: 11, color: EeccColors.textSecondary),
                      ),
                    ],
                  ),
                ),
                IconButton(
                  icon: const Icon(Icons.play_circle_fill_rounded, color: EeccColors.primary, size: 32),
                  onPressed: () => widget.onNavigateTab(2),
                ),
              ],
            ),
          );
        }).toList(),
      ],
    );
  }

  Widget _buildPlatformBadge(String platform) {
    Color color = Colors.red;
    String label = 'YouTube';

    if (platform == 'facebook') {
      color = const Color(0xFF1877F2);
      label = 'Facebook';
    } else if (platform == 'tiktok') {
      color = Colors.black;
      label = 'TikTok';
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(4),
      ),
      child: Text(
        label,
        style: const TextStyle(color: Colors.white, fontSize: 9, fontWeight: FontWeight.bold),
      ),
    );
  }
}
