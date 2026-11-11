/// Modèle de replay externe conforme à la règle fondamentale (Section 4.3) :
/// EECC TV n'héberge pas la vidéo de replay, mais référence les liens externes
/// vers Facebook, YouTube, TikTok ou autres plateformes.
class ReplayItem {
  final String id;
  final String eventId;
  final String title;
  final String platform; // 'youtube', 'facebook', 'tiktok', etc.
  final String url;
  final String? externalId;
  final String? speaker;
  final DateTime date;
  final String? durationText;
  final String availabilityStatus; // 'available', 'unverified', 'unavailable'

  ReplayItem({
    required this.id,
    required this.eventId,
    required this.title,
    required this.platform,
    required this.url,
    this.externalId,
    this.speaker,
    required this.date,
    this.durationText,
    this.availabilityStatus = 'available',
  });

  factory ReplayItem.fromJson(Map<String, dynamic> json) {
    return ReplayItem(
      id: json['id'] as String,
      eventId: json['event_id'] as String,
      title: json['title'] as String? ?? 'Culte EECC',
      platform: json['platform'] as String? ?? 'youtube',
      url: json['url'] as String,
      externalId: json['external_id'] as String?,
      speaker: json['speaker'] as String?,
      date: json['date'] != null ? DateTime.parse(json['date'] as String) : DateTime.now(),
      durationText: json['duration_text'] as String?,
      availabilityStatus: json['availability_status'] as String? ?? 'available',
    );
  }
}
