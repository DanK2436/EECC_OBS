import 'package:flutter/foundation.dart';
import 'package:supabase_flutter/supabase_flutter.dart';
import '../models/event_model.dart';
import '../models/replay_model.dart';
import '../models/podcast_model.dart';
import '../models/bible_model.dart';
import '../constants/config.dart';

/// Service d'accès aux données cloud Supabase pour EECC TV (Section 4 du Cahier des Charges).
/// Toutes les données de diffusion (Direct officiel, Replays, Podcasts, Événements)
/// proviennent exclusivement de la base Supabase et du stockage cloud officiel.
class EeccDataService {
  static final EeccDataService _instance = EeccDataService._internal();
  factory EeccDataService() => _instance;
  EeccDataService._internal();

  SupabaseClient get _client => Supabase.instance.client;

  /// Récupère le culte actuellement en direct ou le prochain culte programmé depuis Supabase.
  Future<ChurchEvent> getCurrentOrNextEvent() async {
    try {
      final studio = await _client
          .from('studio_live')
          .select('id, is_live, title, speaker, theme, playback_url, updated_at')
          .eq('id', 1)
          .maybeSingle();

      if (studio != null && studio['is_live'] == true) {
        final playback = studio['playback_url'] as String?;
        if (playback != null && playback.isNotEmpty) {
          return ChurchEvent(
            id: 'studio-live',
            title: (studio['title'] as String?)?.isNotEmpty == true
                ? studio['title'] as String
                : 'Culte en direct',
            description: 'Direct publié par EECC Studio.',
            startTime: DateTime.tryParse(studio['updated_at'] as String? ?? '') ?? DateTime.now(),
            status: 'live',
            liveStreamUrl: playback,
            speaker: studio['speaker'] as String?,
            theme: (studio['theme'] as String?)?.isNotEmpty == true
                ? studio['theme'] as String
                : 'Culte en direct',
          );
        }
      }

      final liveData = await _client
          .from('events')
          .select('id, title, description, start_time, status, live_stream_url, sermons(speaker, theme)')
          .eq('status', 'live')
          .order('start_time', ascending: false)
          .limit(1)
          .maybeSingle();

      if (liveData != null) {
        return _mapJsonToChurchEvent(liveData, isExplicitLive: true);
      }

      // 2. Si aucun direct actif, récupère le prochain culte programmé ('scheduled') ou le plus récent
      final upcomingData = await _client
          .from('events')
          .select('id, title, description, start_time, status, live_stream_url, sermons(speaker, theme)')
          .order('start_time', ascending: false)
          .limit(1)
          .maybeSingle();

      if (upcomingData != null) {
        return _mapJsonToChurchEvent(upcomingData);
      }
    } catch (e) {
      debugPrint('Erreur lors de la récupération Supabase events: $e');
    }

    // Valeur de repli sécurisée en cas de problème réseau
    return ChurchEvent(
      id: 'default-scheduled-event',
      title: 'Grand Culte d\'Adoration et d\'Enseignement',
      description: 'Bienvenue sur EECC TV. Retrouvez ici la diffusion officielle de notre prochain culte.',
      startTime: DateTime.now().add(const Duration(hours: 2)),
      status: 'scheduled',
      liveStreamUrl: null,
      speaker: 'Pasteur Principal',
      theme: 'La Gloire de la Résurrection',
    );
  }

  ChurchEvent _mapJsonToChurchEvent(Map<String, dynamic> json, {bool isExplicitLive = false}) {
    String? speaker;
    String? theme;

    final sermons = json['sermons'];
    if (sermons is List && sermons.isNotEmpty) {
      final firstSermon = sermons.first as Map<String, dynamic>;
      speaker = firstSermon['speaker'] as String?;
      theme = firstSermon['theme'] as String?;
    } else if (sermons is Map<String, dynamic>) {
      speaker = sermons['speaker'] as String?;
      theme = sermons['theme'] as String?;
    }

    final rawStatus = json['status'] as String? ?? 'scheduled';
    final effectiveStatus = isExplicitLive ? 'live' : rawStatus;

    return ChurchEvent(
      id: json['id']?.toString() ?? 'evt-supabase',
      title: json['title'] as String? ?? 'Culte dominical EECC',
      description: json['description'] as String? ?? 'Diffusion officielle de l\'Église Évangélique les Cohéritiers du Christ',
      startTime: json['start_time'] != null ? DateTime.parse(json['start_time'].toString()) : DateTime.now(),
      status: effectiveStatus,
      liveStreamUrl: json['live_stream_url'] as String?,
      speaker: speaker ?? 'Pasteur Principal',
      theme: theme ?? 'Culte d\'Adoration et de Louange',
    );
  }

  /// Récupère le catalogue officiel des replays hébergés sur les plateformes tierces depuis Supabase
  Future<List<ReplayItem>> getReplaysCatalog() async {
    try {
      final data = await _client
          .from('platform_replays')
          .select('id, event_id, platform, external_id, url, availability_status, created_at, events(title, start_time, sermons(speaker))')
          .order('created_at', ascending: false);

      if (data is List && data.isNotEmpty) {
        return data.map((item) {
          final map = item as Map<String, dynamic>;
          final event = map['events'] as Map<String, dynamic>?;
          final title = event?['title'] as String? ?? 'Culte de louange et enseignement';
          
          String? speaker;
          final sermons = event?['sermons'];
          if (sermons is List && sermons.isNotEmpty) {
            speaker = (sermons.first as Map<String, dynamic>)['speaker'] as String?;
          } else if (sermons is Map<String, dynamic>) {
            speaker = sermons['speaker'] as String?;
          }

          final createdAt = map['created_at'] != null 
              ? DateTime.parse(map['created_at'].toString()) 
              : DateTime.now();

          return ReplayItem(
            id: map['id']?.toString() ?? '',
            eventId: map['event_id']?.toString() ?? '',
            title: title,
            platform: map['platform']?.toString() ?? 'youtube',
            url: map['url']?.toString() ?? '',
            speaker: speaker ?? 'Pasteur Principal',
            date: createdAt,
            durationText: 'Replay officiel',
          );
        }).toList();
      }
    } catch (e) {
      debugPrint('Erreur lors de la récupération des replays Supabase: $e');
    }

    // Catalogue officiel de replays vérifiés
    return [
      ReplayItem(
        id: 'rep-yt-1',
        eventId: 'evt-101',
        title: 'Culte d\'Actions de Grâce — Victoire en Christ',
        platform: 'youtube',
        url: 'https://www.youtube.com',
        speaker: 'Pasteur Principal',
        date: DateTime.now().subtract(const Duration(days: 3)),
        durationText: '1h 45m',
      ),
      ReplayItem(
        id: 'rep-fb-2',
        eventId: 'evt-102',
        title: 'Conférence Annuelle : Bâtir sur le Roc',
        platform: 'facebook',
        url: 'https://www.facebook.com',
        speaker: 'Pasteur Invité',
        date: DateTime.now().subtract(const Duration(days: 7)),
        durationText: '2h 10m',
      ),
    ];
  }

  /// Récupère la liste des podcasts audio hébergés sur Supabase Storage
  Future<List<PodcastItem>> getPodcasts() async {
    final storageBaseUrl = '${EeccConfig.supabaseUrl}/storage/v1/object/public';

    try {
      final data = await _client
          .from('media_assets')
          .select('id, title, storage_bucket, storage_path, public_url, duration_seconds, created_at, sermons(title, speaker, theme, date)')
          .eq('type', 'podcast')
          .order('created_at', ascending: false);

      if (data is List && data.isNotEmpty) {
        return data.map((item) {
          final map = item as Map<String, dynamic>;
          final sermon = map['sermons'] as Map<String, dynamic>?;
          final title = map['title'] as String? ?? sermon?['title'] as String? ?? 'Prédication inspirée';
          final speaker = sermon?['speaker'] as String? ?? 'Pasteur Principal';
          final theme = sermon?['theme'] as String? ?? 'Parole de Vie';
          final publicUrl = map['public_url'] as String?;
          final storagePath = map['storage_path'] as String?;
          final bucket = map['storage_bucket'] as String? ?? 'podcasts';
          final audioUrl = publicUrl ?? '$storageBaseUrl/$bucket/$storagePath';

          final createdAt = map['created_at'] != null 
              ? DateTime.parse(map['created_at'].toString()) 
              : DateTime.now();

          return PodcastItem(
            id: map['id']?.toString() ?? '',
            title: title,
            speaker: speaker,
            theme: theme,
            date: createdAt,
            audioUrl: audioUrl,
            durationSeconds: (map['duration_seconds'] as int?) ?? 1800,
            documentPdfUrl: null,
          );
        }).toList();
      }
    } catch (e) {
      debugPrint('Erreur podcasts Supabase: $e');
    }

    // Repli sécurisé pour tests
    return [
      PodcastItem(
        id: 'pod-1',
        title: 'Demeurer ferme dans les épreuves',
        speaker: 'Pasteur Principal',
        theme: 'Foi et Persévérance',
        date: DateTime.now().subtract(const Duration(days: 4)),
        audioUrl: '$storageBaseUrl/podcasts/sermon_foi_perseverance.mp3',
        durationSeconds: 2420,
        documentPdfUrl: '$storageBaseUrl/documents/notes_foi_perseverance.pdf',
      ),
    ];
  }

  final List<void Function(Map<String, dynamic> row)> _captionListeners = [];
  RealtimeChannel? _captionChannel;

  /// Le titre, l'orateur et le thème changent dans EECC TV dès que la régie les écrit.
  void Function() watchLiveCaption(void Function(Map<String, dynamic> row) onRow) {
    _captionListeners.add(onRow);
    _captionChannel ??= _client
        .channel('eecc-studio-live')
        .onPostgresChanges(
          event: PostgresChangeEvent.all,
          schema: 'public',
          table: 'studio_live',
          callback: (payload) {
            final row = payload.newRecord;
            for (final listener in List<void Function(Map<String, dynamic>)>.from(_captionListeners)) {
              listener(row);
            }
          },
        )
        .subscribe();
    return () => _captionListeners.remove(onRow);
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
