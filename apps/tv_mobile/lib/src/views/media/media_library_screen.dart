import 'package:flutter/material.dart';
import '../../constants/colors.dart';
import '../../models/podcast_model.dart';
import '../../services/supabase_service.dart';
import 'package:url_launcher/url_launcher.dart';

class MediaLibraryScreen extends StatefulWidget {
  const MediaLibraryScreen({super.key});

  @override
  State<MediaLibraryScreen> createState() => _MediaLibraryScreenState();
}

class _MediaLibraryScreenState extends State<MediaLibraryScreen> {
  final EeccDataService _dataService = EeccDataService();
  List<PodcastItem> _podcasts = [];
  bool _isLoading = true;
  String? _currentlyPlayingId;
  bool _isPlaying = false;

  @override
  void initState() {
    super.initState();
    _loadPodcasts();
  }

  Future<void> _loadPodcasts() async {
    final list = await _dataService.getPodcasts();
    if (mounted) {
      setState(() {
        _podcasts = list;
        _isLoading = false;
      });
    }
  }

  void _togglePlayPodcast(PodcastItem podcast) {
    setState(() {
      if (_currentlyPlayingId == podcast.id) {
        _isPlaying = !_isPlaying;
      } else {
        _currentlyPlayingId = podcast.id;
        _isPlaying = true;
      }
    });
  }

  Future<void> _openDocument(String url) async {
    final uri = Uri.parse(url);
    if (await canLaunchUrl(uri)) {
      await launchUrl(uri, mode: LaunchMode.externalApplication);
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
          'Médiathèque & Podcasts',
          style: TextStyle(fontWeight: FontWeight.bold, color: EeccColors.textMain),
        ),
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator(color: EeccColors.primary))
          : Column(
              children: [
                Expanded(
                  child: ListView.builder(
                    padding: const EdgeInsets.all(16),
                    itemCount: _podcasts.length,
                    itemBuilder: (context, index) {
                      final podcast = _podcasts[index];
                      final isCurrent = _currentlyPlayingId == podcast.id;

                      return Container(
                        margin: const EdgeInsets.only(bottom: 14),
                        padding: const EdgeInsets.all(16),
                        decoration: BoxDecoration(
                          color: EeccColors.surface,
                          borderRadius: BorderRadius.circular(16),
                          border: Border.all(
                            color: isCurrent ? EeccColors.primaryLight : EeccColors.border,
                            width: isCurrent ? 2 : 1,
                          ),
                          boxShadow: [
                            BoxShadow(
                              color: Colors.black.withOpacity(0.03),
                              blurRadius: 8,
                              offset: const Offset(0, 2),
                            ),
                          ],
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Container(
                                  padding: const EdgeInsets.all(12),
                                  decoration: BoxDecoration(
                                    color: (isCurrent ? EeccColors.primary : EeccColors.slate).withOpacity(0.1),
                                    borderRadius: BorderRadius.circular(12),
                                  ),
                                  child: Icon(
                                    isCurrent && _isPlaying ? Icons.pause_rounded : Icons.play_arrow_rounded,
                                    color: isCurrent ? EeccColors.primary : EeccColors.slate,
                                    size: 28,
                                  ),
                                ),
                                const SizedBox(width: 14),
                                Expanded(
                                  child: Column(
                                    crossAxisAlignment: CrossAxisAlignment.start,
                                    children: [
                                      Text(
                                        podcast.title,
                                        style: const TextStyle(
                                          fontWeight: FontWeight.bold,
                                          fontSize: 15,
                                          color: EeccColors.textMain,
                                        ),
                                      ),
                                      const SizedBox(height: 4),
                                      Text(
                                        '${podcast.speaker} • ${podcast.formattedDuration}',
                                        style: const TextStyle(fontSize: 12, color: EeccColors.textSecondary),
                                      ),
                                    ],
                                  ),
                                ),
                                IconButton(
                                  icon: Icon(
                                    isCurrent && _isPlaying
                                        ? Icons.pause_circle_filled_rounded
                                        : Icons.play_circle_fill_rounded,
                                    color: EeccColors.primary,
                                    size: 40,
                                  ),
                                  onPressed: () => _togglePlayPodcast(podcast),
                                ),
                              ],
                            ),
                            if (podcast.documentPdfUrl != null) ...[
                              const SizedBox(height: 12),
                              const Divider(height: 1, color: EeccColors.border),
                              const SizedBox(height: 10),
                              Row(
                                children: [
                                  const Icon(Icons.picture_as_pdf_rounded, size: 16, color: Colors.redAccent),
                                  const SizedBox(width: 6),
                                  const Text('Notes de prédication (PDF)', style: TextStyle(fontSize: 12, color: EeccColors.slate)),
                                  const Spacer(),
                                  TextButton(
                                    style: TextButton.styleFrom(padding: EdgeInsets.zero),
                                    onPressed: () => _openDocument(podcast.documentPdfUrl!),
                                    child: const Text('Télécharger', style: TextStyle(fontWeight: FontWeight.bold, color: EeccColors.primary)),
                                  ),
                                ],
                              ),
                            ],
                          ],
                        ),
                      );
                    },
                  ),
                ),

                // Mini Lecteur Flottant en bas si un podcast est en cours
                if (_currentlyPlayingId != null) _buildMiniPlayer(),
              ],
            ),
    );
  }

  Widget _buildMiniPlayer() {
    final current = _podcasts.firstWhere((p) => p.id == _currentlyPlayingId);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: EeccColors.primaryDark,
        boxShadow: [
          BoxShadow(color: Colors.black.withOpacity(0.2), blurRadius: 10, offset: const Offset(0, -3)),
        ],
      ),
      child: SafeArea(
        top: false,
        child: Row(
          children: [
            const Icon(Icons.graphic_eq_rounded, color: Colors.white, size: 28),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    current.title,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 13),
                  ),
                  Text(
                    current.speaker,
                    style: const TextStyle(color: Colors.white70, fontSize: 11),
                  ),
                ],
              ),
            ),
            IconButton(
              icon: Icon(_isPlaying ? Icons.pause_rounded : Icons.play_arrow_rounded, color: Colors.white),
              onPressed: () => setState(() => _isPlaying = !_isPlaying),
            ),
            IconButton(
              icon: const Icon(Icons.close_rounded, color: Colors.white70),
              onPressed: () => setState(() => _currentlyPlayingId = null),
            ),
          ],
        ),
      ),
    );
  }
}
