import '../models/event_model.dart';
import '../models/podcast_model.dart';
import '../models/replay_model.dart';

class ChurchPlace {
  final String id;
  final String name;
  final String? address;
  final String? city;

  const ChurchPlace({required this.id, required this.name, this.address, this.city});
}

class TvSnapshot {
  final ChurchEvent? event;
  final List<ReplayItem> replays;
  final List<PodcastItem> podcasts;
  final List<ChurchPlace> churches;
  final String? error;

  const TvSnapshot({
    this.event,
    this.replays = const [],
    this.podcasts = const [],
    this.churches = const [],
    this.error,
  });
}

bool isPlaceholderMediaUrl(String? url) {
  if (url == null) return true;
  final trimmed = url.trim();
  if (trimmed.isEmpty) return true;
  final uri = Uri.tryParse(trimmed);
  if (uri == null || uri.host.isEmpty) return true;
  final host = uri.host.toLowerCase();
  if (host == 'test-streams.mux.dev') return true;
  const bareHosts = {
    'youtube.com',
    'www.youtube.com',
    'youtu.be',
    'facebook.com',
    'www.facebook.com',
    'tiktok.com',
    'www.tiktok.com',
  };
  final path = uri.path;
  return bareHosts.contains(host) && (path.isEmpty || path == '/');
}

ChurchEvent? eventFromRow(Map<String, dynamic> row) {
  final id = row['id']?.toString();
  final title = row['title']?.toString();
  final start = DateTime.tryParse(row['start_time']?.toString() ?? '');
  if (id == null || id.isEmpty || title == null || title.isEmpty || start == null) return null;
  final stream = row['live_stream_url']?.toString();
  return ChurchEvent(
    id: id,
    title: title,
    description: _text(row['description']),
    startTime: start,
    endTime: DateTime.tryParse(row['end_time']?.toString() ?? ''),
    status: row['status']?.toString() ?? 'scheduled',
    liveStreamUrl: stream != null && !isPlaceholderMediaUrl(stream) ? stream : null,
    speaker: _text(row['speaker']),
    theme: _text(row['theme']),
    bibleReference: _text(row['bible_reference']),
  );
}

ChurchEvent? pickDisplayedEvent(List<ChurchEvent> events, DateTime now) {
  final live = events.where((event) => event.status == 'live').toList();
  if (live.isNotEmpty) return live.first;
  final upcoming = events.where((event) => event.startTime.isAfter(now) && event.status != 'ended').toList()
    ..sort((a, b) => a.startTime.compareTo(b.startTime));
  if (upcoming.isNotEmpty) return upcoming.first;
  return null;
}

ReplayItem? replayFromRow(Map<String, dynamic> row) {
  final url = row['url']?.toString() ?? '';
  if (isPlaceholderMediaUrl(url)) return null;
  final id = row['id']?.toString();
  final eventId = row['event_id']?.toString();
  if (id == null || eventId == null) return null;
  final event = row['events'];
  var title = 'Replay sans titre';
  DateTime? date;
  if (event is Map) {
    final eventTitle = event['title']?.toString();
    if (eventTitle != null && eventTitle.isNotEmpty) title = eventTitle;
    date = DateTime.tryParse(event['start_time']?.toString() ?? '');
  }
  return ReplayItem(
    id: id,
    eventId: eventId,
    title: title,
    platform: row['platform']?.toString() ?? 'plateforme',
    url: url,
    externalId: row['external_id']?.toString(),
    date: date,
    availabilityStatus: row['availability_status']?.toString() ?? 'unverified',
  );
}

PodcastItem? podcastFromRow(Map<String, dynamic> row) {
  final id = row['id']?.toString();
  if (id == null) return null;
  final sermon = row['sermons'];
  final title = row['title']?.toString() ?? (sermon is Map ? sermon['title']?.toString() : null);
  if (title == null || title.isEmpty) return null;
  final download = row['drive_download_url']?.toString();
  final web = row['drive_web_url']?.toString();
  final audio = (download != null && download.isNotEmpty) ? download : (web ?? '');
  DateTime date = DateTime.fromMillisecondsSinceEpoch(0, isUtc: true);
  String? speaker;
  String? theme;
  if (sermon is Map) {
    speaker = _text(sermon['speaker']);
    theme = _text(sermon['theme']);
    date = DateTime.tryParse(sermon['date']?.toString() ?? '') ?? date;
  }
  return PodcastItem(
    id: id,
    title: title,
    speaker: speaker ?? 'Intervenant non indiqué',
    theme: theme,
    date: date,
    audioUrl: isPlaceholderMediaUrl(audio) ? '' : audio,
    durationSeconds: row['duration_seconds'] is int ? row['duration_seconds'] as int : 0,
    documentPdfUrl: null,
  );
}

ChurchPlace? churchFromRow(Map<String, dynamic> row) {
  final id = row['id']?.toString();
  final name = row['name']?.toString();
  if (id == null || name == null || name.isEmpty) return null;
  return ChurchPlace(
    id: id,
    name: name,
    address: _text(row['address']),
    city: _text(row['city']),
  );
}

String? _text(Object? value) {
  final text = value?.toString().trim();
  if (text == null || text.isEmpty) return null;
  return text;
}
