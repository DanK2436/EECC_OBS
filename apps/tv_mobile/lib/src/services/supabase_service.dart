import '../models/event_model.dart';
import '../models/replay_model.dart';
import '../models/podcast_model.dart';
import '../models/bible_model.dart';
import '../constants/config.dart';

/// Service d'accès aux données Supabase pour EECC TV
/// Utilise Supabase Storage pour les podcasts audio et documents
class EeccDataService {
  static final EeccDataService _instance = EeccDataService._internal();
  factory EeccDataService() => _instance;
  EeccDataService._internal();

  /// Récupère le culte actuellement en direct ou le prochain culte programmé
  Future<ChurchEvent> getCurrentOrNextEvent() async {
    return ChurchEvent(
      id: 'demo-live-1',
      title: 'Grand Culte d\'Adoration et d\'Enseignement',
      description: 'Rejoignez la communauté EECC pour un temps puissant dans la présence de Dieu.',
      startTime: DateTime.now().add(const Duration(minutes: 30)),
      status: 'scheduled',
      liveStreamUrl: 'https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8',
      speaker: 'Pasteur Principal',
      theme: 'La Puissance de la Foi Agissante',
    );
  }

  /// Récupère le catalogue des replays externes (Section 4.3)
  Future<List<ReplayItem>> getReplaysCatalog() async {
    return [
      ReplayItem(
        id: 'rep-1',
        eventId: 'evt-101',
        title: 'Culte d\'Actions de Grâce — Victoire en Christ',
        platform: 'youtube',
        url: 'https://www.youtube.com',
        speaker: 'Pasteur Principal',
        date: DateTime.now().subtract(const Duration(days: 3)),
        durationText: '1h 45m',
      ),
      ReplayItem(
        id: 'rep-2',
        eventId: 'evt-102',
        title: 'Conférence Annuelle : Bâtir sur le Roc',
        platform: 'facebook',
        url: 'https://www.facebook.com',
        speaker: 'Pasteur Invité',
        date: DateTime.now().subtract(const Duration(days: 7)),
        durationText: '2h 10m',
      ),
      ReplayItem(
        id: 'rep-3',
        eventId: 'evt-103',
        title: 'Moments d\'Adoration et Louange Vivante',
        platform: 'tiktok',
        url: 'https://www.tiktok.com',
        speaker: 'Groupe de Louange EECC',
        date: DateTime.now().subtract(const Duration(days: 10)),
        durationText: '45m',
      ),
    ];
  }

  /// Récupère la liste des podcasts audio hébergés sur Supabase Storage
  Future<List<PodcastItem>> getPodcasts() async {
    final storageBaseUrl = '${EeccConfig.supabaseUrl}/storage/v1/object/public';

    return [
      PodcastItem(
        id: 'pod-1',
        title: 'Demeurer ferme dans les épreuves',
        speaker: 'Pasteur Principal',
        theme: 'Foi et Persévérance',
        date: DateTime.now().subtract(const Duration(days: 4)),
        audioUrl: '$storageBaseUrl/podcasts/2026/11/sermon_foi_perseverance.mp3',
        durationSeconds: 2420,
        documentPdfUrl: '$storageBaseUrl/documents/2026/11/notes_foi_perseverance.pdf',
      ),
      PodcastItem(
        id: 'pod-2',
        title: 'La prière qui transforme les nations',
        speaker: 'Pasteur Associé',
        theme: 'Intercession',
        date: DateTime.now().subtract(const Duration(days: 11)),
        audioUrl: '$storageBaseUrl/podcasts/2026/11/sermon_priere_puissante.mp3',
        durationSeconds: 1850,
        documentPdfUrl: '$storageBaseUrl/documents/2026/11/resume_priere.pdf',
      ),
    ];
  }

  /// Récupère les livres bibliques
  Future<List<BibleBook>> getBibleBooks() async {
    return [
      BibleBook(id: 1, code: 'GEN', name: 'Genèse', testament: 'OT', chapterCount: 50),
      BibleBook(id: 19, code: 'PSA', name: 'Psaumes', testament: 'OT', chapterCount: 150),
      BibleBook(id: 20, code: 'PRO', name: 'Proverbes', testament: 'OT', chapterCount: 31),
      BibleBook(id: 40, code: 'MAT', name: 'Matthieu', testament: 'NT', chapterCount: 28),
      BibleBook(id: 43, code: 'JHN', name: 'Jean', testament: 'NT', chapterCount: 21),
      BibleBook(id: 45, code: 'ROM', name: 'Romains', testament: 'NT', chapterCount: 16),
      BibleBook(id: 66, code: 'REV', name: 'Apocalypse', testament: 'NT', chapterCount: 22),
    ];
  }
}
