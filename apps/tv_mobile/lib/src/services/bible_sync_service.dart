import 'package:supabase_flutter/supabase_flutter.dart';

class BibleBookmark {
  final String book;
  final int chapter;
  final int verse;
  final String? note;
  final DateTime createdAt;

  BibleBookmark({
    required this.book,
    required this.chapter,
    required this.verse,
    this.note,
    required this.createdAt,
  });

  Map<String, dynamic> toJson() => {
    'book': book,
    'chapter': chapter,
    'verse': verse,
    'note': note,
    'created_at': createdAt.toIso8601String(),
  };
}

/// Service de synchronisation des favoris et signets bibliques avec le profil Supabase
class BibleSyncService {
  final SupabaseClient? _client;
  final List<BibleBookmark> _localBookmarks = [];

  BibleSyncService([this._client]);

  List<BibleBookmark> get bookmarks => List.unmodifiable(_localBookmarks);

  Future<void> addBookmark(String book, int chapter, int verse, {String? note}) async {
    final bookmark = BibleBookmark(
      book: book,
      chapter: chapter,
      verse: verse,
      note: note,
      createdAt: DateTime.now(),
    );

    _localBookmarks.add(bookmark);

    if (_client != null && _client!.auth.currentUser != null) {
      try {
        await _client!.from('user_bible_bookmarks').insert({
          'user_id': _client!.auth.currentUser!.id,
          ...bookmark.toJson(),
        });
      } catch (_) {}
    }
  }
}
