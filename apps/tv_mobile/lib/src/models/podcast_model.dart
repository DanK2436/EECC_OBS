class PodcastItem {
  final String id;
  final String title;
  final String speaker;
  final String? theme;
  final DateTime date;
  final String audioUrl;
  final int durationSeconds;
  final String? documentPdfUrl;

  PodcastItem({
    required this.id,
    required this.title,
    required this.speaker,
    this.theme,
    required this.date,
    required this.audioUrl,
    required this.durationSeconds,
    this.documentPdfUrl,
  });

  String get formattedDuration {
    final minutes = durationSeconds ~/ 60;
    final seconds = durationSeconds % 60;
    return '${minutes.toString().padLeft(2, '0')}:${seconds.toString().padLeft(2, '0')}';
  }

  factory PodcastItem.fromJson(Map<String, dynamic> json) {
    return PodcastItem(
      id: json['id'] as String,
      title: json['title'] as String,
      speaker: json['speaker'] as String? ?? 'Intervenant non indiqué',
      theme: json['theme'] as String?,
      date: json['date'] != null ? DateTime.parse(json['date'] as String) : DateTime.now(),
      audioUrl: json['audio_url'] as String? ?? '',
      durationSeconds: json['duration_seconds'] as int? ?? 0,
      documentPdfUrl: json['document_pdf_url'] as String?,
    );
  }
}
