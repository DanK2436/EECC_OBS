import 'package:flutter/material.dart';
import 'package:just_audio/just_audio.dart';
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
  final AudioPlayer _player = AudioPlayer();
  List<PodcastItem> _podcasts = [];
  bool _isLoading = true;
  String? _currentlyPlayingId;
  String? _error;

  @override
  void initState() {
    super.initState();
    _loadPodcasts();
  }

  @override
  void dispose() {
    _player.dispose();
    super.dispose();
  }

  Future<void> _loadPodcasts() async {
    final snapshot = await _dataService.loadCatalog();
    if (mounted) {
      setState(() {
        _podcasts = snapshot.podcasts;
        _error = snapshot.error;
        _isLoading = false;
      });
    }
  }

  Future<void> _togglePlayPodcast(PodcastItem podcast) async {
    if (podcast.audioUrl.isEmpty) {
      setState(() => _error = 'Aucun fichier audio n\'est enregistré pour « ${podcast.title} ».');
      return;
    }
    try {
      if (_currentlyPlayingId == podcast.id && _player.playing) {
        await _player.pause();
      } else {
        await _player.setUrl(podcast.audioUrl);
        await _player.play();
        _currentlyPlayingId = podcast.id;
      }
      if (mounted) setState(() => _error = null);
    } catch (_) {
      if (mounted) setState(() => _error = 'La lecture de « ${podcast.title} » a échoué.');
    }
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
                if (_error != null)
                  Padding(
                    padding: const EdgeInsets.fromLTRB(16, 12, 16, 0),
                    child: Text(_error!, style: const TextStyle(color: EeccColors.warningOrange)),
                  ),
                if (_podcasts.isEmpty)
                  const Expanded(
                    child: Center(child: Text('Aucun sermon audio enregistré.', style: TextStyle(color: EeccColors.textSecondary))),
                  )
                else
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
                                    isCurrent && _player.playing ? Icons.pause_rounded : Icons.play_arrow_rounded,
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
                                    isCurrent && _player.playing
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
              icon: Icon(_player.playing ? Icons.pause_rounded : Icons.play_arrow_rounded, color: Colors.white),
              onPressed: () => _togglePlayPodcast(current),
            ),
            IconButton(
              icon: const Icon(Icons.close_rounded, color: Colors.white70),
              onPressed: () async {
                await _player.stop();
                if (mounted) setState(() => _currentlyPlayingId = null);
              },
            ),
          ],
        ),
      ),
    );
  }
}
