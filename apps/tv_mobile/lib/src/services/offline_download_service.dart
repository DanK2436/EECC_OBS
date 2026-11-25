import 'dart:io';
import 'package:http/http.dart' as http;
import 'package:path_provider/path_provider.dart';

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
      final dir = await getApplicationDocumentsDirectory();
      final targetPath = '${dir.path}/eecc_offline_${id}.$fileExtension';

      final file = File(targetPath);
      if (await file.exists()) {
        return targetPath;
      }

      final res = await http.get(Uri.parse(url));
      if (res.statusCode == 200) {
        await file.writeAsBytes(res.bodyBytes);
        _downloads.add(DownloadItem(
          id: id,
          title: title,
          localPath: targetPath,
          downloadedAt: DateTime.now(),
        ));
        return targetPath;
      }
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
