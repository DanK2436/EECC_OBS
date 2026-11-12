class ChurchEvent {
  final String id;
  final String title;
  final String? description;
  final DateTime startTime;
  final DateTime? endTime;
  final String status; // 'scheduled', 'live', 'ended'
  final String? liveStreamUrl;
  final String? speaker;
  final String? theme;
  final String? bibleReference;

  ChurchEvent({
    required this.id,
    required this.title,
    this.description,
    required this.startTime,
    this.endTime,
    required this.status,
    this.liveStreamUrl,
    this.speaker,
    this.theme,
    this.bibleReference,
  });

  bool get isLive => status == 'live';

  String get dateFormatted {
    const weekdays = [
      'Lundi',
      'Mardi',
      'Mercredi',
      'Jeudi',
      'Vendredi',
      'Samedi',
      'Dimanche',
    ];
    const months = [
      'janvier',
      'février',
      'mars',
      'avril',
      'mai',
      'juin',
      'juillet',
      'août',
      'septembre',
      'octobre',
      'novembre',
      'décembre',
    ];
    final local = startTime.toLocal();
    return '${weekdays[local.weekday - 1]} ${local.day} ${months[local.month - 1]} ${local.year}';
  }

  factory ChurchEvent.fromJson(Map<String, dynamic> json) {
    return ChurchEvent(
      id: json['id'] as String,
      title: json['title'] as String,
      description: json['description'] as String?,
      startTime: DateTime.parse(json['start_time'] as String),
      endTime: json['end_time'] != null ? DateTime.parse(json['end_time'] as String) : null,
      status: json['status'] as String? ?? 'scheduled',
      liveStreamUrl: json['live_stream_url'] as String?,
      speaker: json['speaker'] as String?,
      theme: json['theme'] as String?,
      bibleReference: json['bible_reference'] as String?,
    );
  }

  Map<String, dynamic> toJson() {
    return {
      'id': id,
      'title': title,
      'description': description,
      'start_time': startTime.toIso8601String(),
      'end_time': endTime?.toIso8601String(),
      'status': status,
      'live_stream_url': liveStreamUrl,
      'speaker': speaker,
      'theme': theme,
      'bible_reference': bibleReference,
    };
  }
}
