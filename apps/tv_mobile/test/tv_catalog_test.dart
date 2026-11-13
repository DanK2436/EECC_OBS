import 'package:eecc_tv/src/services/tv_catalog.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  test('rejects demo streams and bare platform homepages', () {
    expect(isPlaceholderMediaUrl('https://test-streams.mux.dev/x36xhzz/x36xhzz.m3u8'), isTrue);
    expect(isPlaceholderMediaUrl('https://www.youtube.com'), isTrue);
    expect(isPlaceholderMediaUrl('https://www.facebook.com'), isTrue);
    expect(isPlaceholderMediaUrl('https://www.tiktok.com/'), isTrue);
    expect(isPlaceholderMediaUrl('https://www.youtube.com/watch?v=abc123'), isFalse);
  });

  test('empty and incomplete rows do not invent a service', () {
    expect(eventFromRow({}), isNull);
    expect(replayFromRow({'id': '1', 'event_id': '2', 'url': 'https://www.youtube.com'}), isNull);
    expect(podcastFromRow({'id': '1', 'title': ''}), isNull);
    expect(pickDisplayedEvent(const [], DateTime.utc(2026, 11, 13)), isNull);
  });

  test('a stored event and replay keep only their own fields', () {
    final event = eventFromRow({
      'id': 'evt-1',
      'title': 'Culte du soir',
      'start_time': '2026-11-15T17:00:00Z',
      'status': 'scheduled',
      'live_stream_url': 'https://test-streams.mux.dev/demo.m3u8',
    });
    expect(event, isNotNull);
    expect(event!.liveStreamUrl, isNull);
    expect(event.speaker, isNull);
    expect(event.theme, isNull);

    final replay = replayFromRow({
      'id': 'rep-1',
      'event_id': 'evt-1',
      'platform': 'youtube',
      'url': 'https://www.youtube.com/watch?v=abc123',
      'availability_status': 'unverified',
      'events': {'title': 'Culte du soir', 'start_time': '2026-11-15T17:00:00Z'},
    });
    expect(replay!.title, 'Culte du soir');
    expect(replay.availabilityStatus, 'unverified');
  });
}
