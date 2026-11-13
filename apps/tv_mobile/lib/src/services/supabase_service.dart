import 'package:supabase_flutter/supabase_flutter.dart';

import '../constants/config.dart';
import '../models/event_model.dart';
import '../models/podcast_model.dart';
import '../models/replay_model.dart';
import 'tv_catalog.dart';

class EeccDataService {
  static final EeccDataService _instance = EeccDataService._internal();
  factory EeccDataService() => _instance;
  EeccDataService._internal();

  SupabaseClient? _client;

  Future<TvSnapshot> loadCatalog({DateTime? now}) async {
    final client = await _clientOrNull();
    if (client == null) {
      return const TvSnapshot(error: 'La source des cultes est indisponible.');
    }
    final clock = now ?? DateTime.now();
    String? error;
    final events = <ChurchEvent>[];
    final replays = <ReplayItem>[];
    final podcasts = <PodcastItem>[];
    final churches = <ChurchPlace>[];

    try {
      final rows = await client.from('events').select();
      for (final row in rows as List) {
        if (row is Map<String, dynamic>) {
          final event = eventFromRow(row);
          if (event != null) events.add(event);
        } else if (row is Map) {
          final event = eventFromRow(Map<String, dynamic>.from(row));
          if (event != null) events.add(event);
        }
      }
    } catch (_) {
      error = 'Les cultes n\'ont pas pu être lus.';
    }

    try {
      final rows = await client.from('platform_replays').select('id, event_id, platform, external_id, url, availability_status, events(title, start_time)');
      for (final row in rows as List) {
        final map = row is Map<String, dynamic> ? row : Map<String, dynamic>.from(row as Map);
        final replay = replayFromRow(map);
        if (replay != null) replays.add(replay);
      }
    } catch (_) {
      error ??= 'Les replays n\'ont pas pu être lus.';
    }

    try {
      final rows = await client.from('media_assets').select('id, title, duration_seconds, drive_web_url, drive_download_url, status, sermons(title, speaker, date, theme)').eq('status', 'ready');
      for (final row in rows as List) {
        final map = row is Map<String, dynamic> ? row : Map<String, dynamic>.from(row as Map);
        final podcast = podcastFromRow(map);
        if (podcast != null) podcasts.add(podcast);
      }
    } catch (_) {
      error ??= 'Les sermons audio n\'ont pas pu être lus.';
    }

    try {
      final rows = await client.from('churches').select('id, name, address, city');
      for (final row in rows as List) {
        final map = row is Map<String, dynamic> ? row : Map<String, dynamic>.from(row as Map);
        final church = churchFromRow(map);
        if (church != null) churches.add(church);
      }
    } catch (_) {
      error ??= 'Les implantations n\'ont pas pu être lues.';
    }

    return TvSnapshot(
      event: pickDisplayedEvent(events, clock),
      replays: replays,
      podcasts: podcasts,
      churches: churches,
      error: error,
    );
  }

  Future<SupabaseClient?> _clientOrNull() async {
    if (_client != null) return _client;
    try {
      _client = Supabase.instance.client;
      return _client;
    } catch (_) {
      try {
        await Supabase.initialize(url: EeccConfig.supabaseUrl, anonKey: EeccConfig.supabaseAnonKey);
        _client = Supabase.instance.client;
        return _client;
      } catch (_) {
        return null;
      }
    }
  }
}
