import 'package:flutter/foundation.dart';
import 'package:supabase_flutter/supabase_flutter.dart';

/// Modèles de données CMS pour l'application mobile EECC TV

class CmsAudioSummary {
  final String id;
  final String title;
  final String speaker;
  final DateTime date;
  final String? description;
  final String audioUrl;
  final int durationSeconds;
  final String? coverImageUrl;

  CmsAudioSummary({
    required this.id,
    required this.title,
    required this.speaker,
    required this.date,
    this.description,
    required this.audioUrl,
    required this.durationSeconds,
    this.coverImageUrl,
  });

  factory CmsAudioSummary.fromJson(Map<String, dynamic> json) {
    return CmsAudioSummary(
      id: json['id']?.toString() ?? '',
      title: json['title'] as String? ?? 'Capsule audio',
      speaker: json['speaker'] as String? ?? 'Pasteur',
      date: json['date'] != null ? DateTime.parse(json['date'].toString()) : DateTime.now(),
      description: json['description'] as String?,
      audioUrl: json['audio_url'] as String? ?? '',
      durationSeconds: (json['duration_seconds'] as num?)?.toInt() ?? 0,
      coverImageUrl: json['cover_image_url'] as String?,
    );
  }
}

class CmsShortVideo {
  final String id;
  final String title;
  final String? speaker;
  final DateTime date;
  final String? description;
  final String videoUrl;
  final String? thumbnailUrl;
  final int durationSeconds;
  final String aspectRatio;

  CmsShortVideo({
    required this.id,
    required this.title,
    this.speaker,
    required this.date,
    this.description,
    required this.videoUrl,
    this.thumbnailUrl,
    required this.durationSeconds,
    required this.aspectRatio,
  });

  factory CmsShortVideo.fromJson(Map<String, dynamic> json) {
    return CmsShortVideo(
      id: json['id']?.toString() ?? '',
      title: json['title'] as String? ?? 'Clip court',
      speaker: json['speaker'] as String?,
      date: json['date'] != null ? DateTime.parse(json['date'].toString()) : DateTime.now(),
      description: json['description'] as String?,
      videoUrl: json['video_url'] as String? ?? '',
      thumbnailUrl: json['thumbnail_url'] as String?,
      durationSeconds: (json['duration_seconds'] as num?)?.toInt() ?? 0,
      aspectRatio: json['aspect_ratio'] as String? ?? '9:16',
    );
  }
}

class CmsSermonText {
  final String id;
  final String title;
  final String speaker;
  final DateTime date;
  final String? theme;
  final String? scriptureRefs;
  final String summary;
  final String? fullTranscript;
  final List<String> keyPoints;
  final int readingTimeMinutes;

  CmsSermonText({
    required this.id,
    required this.title,
    required this.speaker,
    required this.date,
    this.theme,
    this.scriptureRefs,
    required this.summary,
    this.fullTranscript,
    required this.keyPoints,
    required this.readingTimeMinutes,
  });

  factory CmsSermonText.fromJson(Map<String, dynamic> json) {
    List<String> points = [];
    final rawPoints = json['key_points'];
    if (rawPoints is List) {
      points = rawPoints.map((e) => e.toString()).toList();
    }

    return CmsSermonText(
      id: json['id']?.toString() ?? '',
      title: json['title'] as String? ?? 'Prédication',
      speaker: json['speaker'] as String? ?? 'Pasteur',
      date: json['date'] != null ? DateTime.parse(json['date'].toString()) : DateTime.now(),
      theme: json['theme'] as String?,
      scriptureRefs: json['scripture_refs'] as String?,
      summary: json['summary'] as String? ?? '',
      fullTranscript: json['full_transcript'] as String?,
      keyPoints: points,
      readingTimeMinutes: (json['reading_time_minutes'] as num?)?.toInt() ?? 5,
    );
  }
}

class CmsEventBanner {
  final String id;
  final String title;
  final String? subtitle;
  final String imageUrl;
  final String? linkUrl;
  final int displayOrder;

  CmsEventBanner({
    required this.id,
    required this.title,
    this.subtitle,
    required this.imageUrl,
    this.linkUrl,
    required this.displayOrder,
  });

  factory CmsEventBanner.fromJson(Map<String, dynamic> json) {
    return CmsEventBanner(
      id: json['id']?.toString() ?? '',
      title: json['title'] as String? ?? 'Affiche Événement',
      subtitle: json['subtitle'] as String?,
      imageUrl: json['image_url'] as String? ?? '',
      linkUrl: json['link_url'] as String?,
      displayOrder: (json['display_order'] as num?)?.toInt() ?? 0,
    );
  }
}

class CmsPhotoAlbum {
  final String id;
  final String title;
  final String? description;
  final DateTime date;
  final String? coverImageUrl;
  final int photoCount;

  CmsPhotoAlbum({
    required this.id,
    required this.title,
    this.description,
    required this.date,
    this.coverImageUrl,
    required this.photoCount,
  });

  factory CmsPhotoAlbum.fromJson(Map<String, dynamic> json) {
    return CmsPhotoAlbum(
      id: json['id']?.toString() ?? '',
      title: json['title'] as String? ?? 'Album Photos Culte',
      description: json['description'] as String?,
      date: json['date'] != null ? DateTime.parse(json['date'].toString()) : DateTime.now(),
      coverImageUrl: json['cover_image_url'] as String?,
      photoCount: (json['photo_count'] as num?)?.toInt() ?? 0,
    );
  }
}

class CmsAlbumPhoto {
  final String id;
  final String albumId;
  final String photoUrl;
  final String? thumbnailUrl;
  final String? caption;
  final int displayOrder;

  CmsAlbumPhoto({
    required this.id,
    required this.albumId,
    required this.photoUrl,
    this.thumbnailUrl,
    this.caption,
    required this.displayOrder,
  });

  factory CmsAlbumPhoto.fromJson(Map<String, dynamic> json) {
    return CmsAlbumPhoto(
      id: json['id']?.toString() ?? '',
      albumId: json['album_id']?.toString() ?? '',
      photoUrl: json['photo_url'] as String? ?? '',
      thumbnailUrl: json['thumbnail_url'] as String?,
      caption: json['caption'] as String?,
      displayOrder: (json['display_order'] as num?)?.toInt() ?? 0,
    );
  }
}

/// Service Supabase CMS pour EECC TV :
/// Filtre systématiquement les contenus sur `is_published = true` (tout contenu masqué dans Studio disparaît ici)
/// et fournit des abonnements temps-réel.
class SupabaseCmsService {
  static final SupabaseCmsService _instance = SupabaseCmsService._internal();
  factory SupabaseCmsService() => _instance;
  SupabaseCmsService._internal();

  SupabaseClient get _client => Supabase.instance.client;

  /// Récupère les résumés audios (capsules) publiés
  Future<List<CmsAudioSummary>> getPublishedAudioSummaries() async {
    try {
      final res = await _client
          .from('audio_summaries')
          .select('*')
          .eq('is_published', true)
          .order('date', ascending: false);
      if (res is List) {
        return res.map((j) => CmsAudioSummary.fromJson(j as Map<String, dynamic>)).toList();
      }
    } catch (e) {
      debugPrint('Erreur getPublishedAudioSummaries: $e');
    }
    return [];
  }

  /// Récupère les courtes vidéos (shorts/reels) publiées
  Future<List<CmsShortVideo>> getPublishedShortVideos() async {
    try {
      final res = await _client
          .from('short_videos')
          .select('*')
          .eq('is_published', true)
          .order('date', ascending: false);
      if (res is List) {
        return res.map((j) => CmsShortVideo.fromJson(j as Map<String, dynamic>)).toList();
      }
    } catch (e) {
      debugPrint('Erreur getPublishedShortVideos: $e');
    }
    return [];
  }

  /// Récupère les résumés textuels de prédications publiés
  Future<List<CmsSermonText>> getPublishedSermonTexts() async {
    try {
      final res = await _client
          .from('sermons')
          .select('id, title, speaker, date, theme, scripture_refs, summary, full_transcript, key_points, reading_time_minutes')
          .eq('is_published', true)
          .not('summary', 'is', null)
          .order('date', ascending: false);
      if (res is List) {
        return res.map((j) => CmsSermonText.fromJson(j as Map<String, dynamic>)).toList();
      }
    } catch (e) {
      debugPrint('Erreur getPublishedSermonTexts: $e');
    }
    return [];
  }

  /// Récupère les affiches publicitaires et flyers d'événements actifs
  Future<List<CmsEventBanner>> getActiveBanners() async {
    try {
      final res = await _client
          .from('event_banners')
          .select('*')
          .eq('is_published', true)
          .order('display_order', ascending: true);
      if (res is List) {
        return res.map((j) => CmsEventBanner.fromJson(j as Map<String, dynamic>)).toList();
      }
    } catch (e) {
      debugPrint('Erreur getActiveBanners: $e');
    }
    return [];
  }

  /// Récupère les albums photos publiés
  Future<List<CmsPhotoAlbum>> getPublishedPhotoAlbums() async {
    try {
      final res = await _client
          .from('photo_albums')
          .select('*')
          .eq('is_published', true)
          .order('date', ascending: false);
      if (res is List) {
        return res.map((j) => CmsPhotoAlbum.fromJson(j as Map<String, dynamic>)).toList();
      }
    } catch (e) {
      debugPrint('Erreur getPublishedPhotoAlbums: $e');
    }
    return [];
  }

  /// Récupère les photos d'un album spécifique (jusqu'à 200 photos)
  Future<List<CmsAlbumPhoto>> getAlbumPhotos(String albumId) async {
    try {
      final res = await _client
          .from('album_photos')
          .select('*')
          .eq('album_id', albumId)
          .eq('is_published', true)
          .order('display_order', ascending: true);
      if (res is List) {
        return res.map((j) => CmsAlbumPhoto.fromJson(j as Map<String, dynamic>)).toList();
      }
    } catch (e) {
      debugPrint('Erreur getAlbumPhotos: $e');
    }
    return [];
  }

  /// Écoute en temps réel les changements CMS pour rafraîchir l'interface sans rechargement
  RealtimeChannel subscribeToCmsChanges(VoidCallback onDataChanged) {
    final channel = _client.channel('public:cms_changes');
    channel
        .onPostgresChanges(
          event: PostgresChangeEvent.all,
          schema: 'public',
          table: 'audio_summaries',
          callback: (payload) => onDataChanged(),
        )
        .onPostgresChanges(
          event: PostgresChangeEvent.all,
          schema: 'public',
          table: 'short_videos',
          callback: (payload) => onDataChanged(),
        )
        .onPostgresChanges(
          event: PostgresChangeEvent.all,
          schema: 'public',
          table: 'event_banners',
          callback: (payload) => onDataChanged(),
        )
        .onPostgresChanges(
          event: PostgresChangeEvent.all,
          schema: 'public',
          table: 'photo_albums',
          callback: (payload) => onDataChanged(),
        )
        .subscribe();

    return channel;
  }
}
