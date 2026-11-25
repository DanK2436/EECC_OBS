import 'dart:io';

class DownloadItem {
  final String id;
  final String title;
  final String localPath;
  final DateTime downloadedAt;

  DownloadItem({
    required this.id,
    required this.title,
    required this.localPath,
    required this.downloadedAt,
  });
}

/// Service de téléchargement hors-ligne pour les podcasts MP3 et résumés PDF
/// Permet aux fidèles d'écouter les prédications sans connexion internet.
class OfflineDownloadService {
  static final OfflineDownloadService _instance = OfflineDownloadService._internal();
  factory OfflineDownloadService() => _instance;
  OfflineDownloadService._internal();

  final List<DownloadItem> _downloads = [];
  List<DownloadItem> get downloads => List.unmodifiable(_downloads);

  Future<String?> downloadMediaFile({
    required String id,
    required String title,
    required String url,
    required String fileExtension,
  }) async {
    try {
      final baseDir = Directory('${Directory.systemTemp.path}/eecc_offline');
      if (!await baseDir.exists()) {
        await baseDir.create(recursive: true);
      }
      final targetPath = '${baseDir.path}/eecc_offline_${id}.$fileExtension';

      final file = File(targetPath);
      if (await file.exists()) {
        return targetPath;
      }

      final client = HttpClient();
      final request = await client.getUrl(Uri.parse(url));
      final response = await request.close();
      if (response.statusCode == 200) {
        final sink = file.openWrite();
        await response.pipe(sink);
        await sink.close();
        client.close();

        _downloads.add(DownloadItem(
          id: id,
          title: title,
          localPath: targetPath,
          downloadedAt: DateTime.now(),
        ));
        return targetPath;
      }
      client.close();
    } catch (_) {}
    return null;
  }

  Future<void> removeDownload(String id) async {
    final idx = _downloads.indexWhere((d) => d.id == id);
    if (idx != -1) {
      final file = File(_downloads[idx].localPath);
      if (await file.exists()) {
        try { await file.delete(); } catch (_) {}
      }
      _downloads.removeAt(idx);
    }
  }
}
