import 'dart:convert';
import 'package:flutter/services.dart';
import 'package:shared_preferences/shared_preferences.dart';

class LocalBibleBook {
  final String key; // Ex: 'Genesis', 'John'
  final String frenchName; // Ex: 'Genèse', 'Jean'
  final String testament; // 'OT' ou 'NT'
  final int chapterCount;

  LocalBibleBook({
    required this.key,
    required this.frenchName,
    required this.testament,
    required this.chapterCount,
  });
}

class LocalBibleVerse {
  final int number;
  final String text;

  LocalBibleVerse({required this.number, required this.text});
}

class BibleLocalService {
  static final BibleLocalService _instance = BibleLocalService._internal();
  factory BibleLocalService() => _instance;
  BibleLocalService._internal();

  Map<String, dynamic>? _bibleData;
  List<LocalBibleBook>? _cachedBooks;

  // Dictionnaire de traduction des 66 livres de la Bible (Anglais -> Français & Testament)
  static const Map<String, Map<String, String>> _bookMetadata = {
    'Genesis': {'fr': 'Genèse', 'testament': 'OT'},
    'Exodus': {'fr': 'Exode', 'testament': 'OT'},
    'Leviticus': {'fr': 'Lévitique', 'testament': 'OT'},
    'Numbers': {'fr': 'Nombres', 'testament': 'OT'},
    'Deuteronomy': {'fr': 'Deutéronome', 'testament': 'OT'},
    'Joshua': {'fr': 'Josué', 'testament': 'OT'},
    'Judges': {'fr': 'Juges', 'testament': 'OT'},
    'Ruth': {'fr': 'Ruth', 'testament': 'OT'},
    '1 Samuel': {'fr': '1 Samuel', 'testament': 'OT'},
    '2 Samuel': {'fr': '2 Samuel', 'testament': 'OT'},
    '1 Kings': {'fr': '1 Rois', 'testament': 'OT'},
    '2 Kings': {'fr': '2 Rois', 'testament': 'OT'},
    '1 Chronicles': {'fr': '1 Chroniques', 'testament': 'OT'},
    '2 Chronicles': {'fr': '2 Chroniques', 'testament': 'OT'},
    'Ezra': {'fr': 'Esdras', 'testament': 'OT'},
    'Nehemiah': {'fr': 'Néhémie', 'testament': 'OT'},
    'Esther': {'fr': 'Esther', 'testament': 'OT'},
    'Job': {'fr': 'Job', 'testament': 'OT'},
    'Psalms': {'fr': 'Psaumes', 'testament': 'OT'},
    'Proverbs': {'fr': 'Proverbes', 'testament': 'OT'},
    'Ecclesiastes': {'fr': 'Ecclésiaste', 'testament': 'OT'},
    'Song of Songs': {'fr': 'Cantique des Cantiques', 'testament': 'OT'},
    'Isaiah': {'fr': 'Ésaïe', 'testament': 'OT'},
    'Jeremiah': {'fr': 'Jérémie', 'testament': 'OT'},
    'Lamentations': {'fr': 'Lamentations', 'testament': 'OT'},
    'Ezekiel': {'fr': 'Ézéchiel', 'testament': 'OT'},
    'Daniel': {'fr': 'Daniel', 'testament': 'OT'},
    'Hosea': {'fr': 'Osée', 'testament': 'OT'},
    'Joel': {'fr': 'Joël', 'testament': 'OT'},
    'Amos': {'fr': 'Amos', 'testament': 'OT'},
    'Obadiah': {'fr': 'Abdias', 'testament': 'OT'},
    'Jonah': {'fr': 'Jonas', 'testament': 'OT'},
    'Micah': {'fr': 'Michée', 'testament': 'OT'},
    'Nahum': {'fr': 'Nahum', 'testament': 'OT'},
    'Habakkuk': {'fr': 'Habacuc', 'testament': 'OT'},
    'Zephaniah': {'fr': 'Sophonie', 'testament': 'OT'},
    'Haggai': {'fr': 'Aggée', 'testament': 'OT'},
    'Zechariah': {'fr': 'Zacharie', 'testament': 'OT'},
    'Malachi': {'fr': 'Malachie', 'testament': 'OT'},
    'Matthew': {'fr': 'Matthieu', 'testament': 'NT'},
    'Mark': {'fr': 'Marc', 'testament': 'NT'},
    'Luke': {'fr': 'Luc', 'testament': 'NT'},
    'John': {'fr': 'Jean', 'testament': 'NT'},
    'Acts': {'fr': 'Actes des Apôtres', 'testament': 'NT'},
    'Romans': {'fr': 'Romains', 'testament': 'NT'},
    '1 Corinthians': {'fr': '1 Corinthiens', 'testament': 'NT'},
    '2 Corinthians': {'fr': '2 Corinthiens', 'testament': 'NT'},
    'Galatians': {'fr': 'Galates', 'testament': 'NT'},
    'Ephesians': {'fr': 'Éphésiens', 'testament': 'NT'},
    'Philippians': {'fr': 'Philippiens', 'testament': 'NT'},
    'Colossians': {'fr': 'Colossiens', 'testament': 'NT'},
    '1 Thessalonians': {'fr': '1 Thessaloniciens', 'testament': 'NT'},
    '2 Thessalonians': {'fr': '2 Thessaloniciens', 'testament': 'NT'},
    '1 Timothy': {'fr': '1 Timothée', 'testament': 'NT'},
    '2 Timothy': {'fr': '2 Timothée', 'testament': 'NT'},
    'Titus': {'fr': 'Tite', 'testament': 'NT'},
    'Philemon': {'fr': 'Philémon', 'testament': 'NT'},
    'Hebrews': {'fr': 'Hébreux', 'testament': 'NT'},
    'James': {'fr': 'Jacques', 'testament': 'NT'},
    '1 Peter': {'fr': '1 Pierre', 'testament': 'NT'},
    '2 Peter': {'fr': '2 Pierre', 'testament': 'NT'},
    '1 John': {'fr': '1 Jean', 'testament': 'NT'},
    '2 John': {'fr': '2 Jean', 'testament': 'NT'},
    '3 John': {'fr': '3 Jean', 'testament': 'NT'},
    'Jude': {'fr': 'Jude', 'testament': 'NT'},
    'Revelation': {'fr': 'Apocalypse', 'testament': 'NT'},
  };

  /// Charge le fichier JSON de la Bible complet en mémoire locale
  Future<void> init() async {
    if (_bibleData != null) return;
    try {
      final jsonString = await rootBundle.loadString('assets/bible/LSG.json');
      _bibleData = jsonDecode(jsonString) as Map<String, dynamic>;
    } catch (_) {
      // Fallback si le bundle n'est pas encore rechargé
      _bibleData = {};
    }
  }

  static const String _prefBookKey = 'eecc_bible_last_book';
  static const String _prefChapterKey = 'eecc_bible_last_chapter';

  /// Sauvegarde la position exacte de lecture de la Bible
  Future<void> saveReadingPosition(String bookKey, int chapter) async {
    try {
      final prefs = await SharedPreferences.getInstance();
      await prefs.setString(_prefBookKey, bookKey);
      await prefs.setInt(_prefChapterKey, chapter);
    } catch (_) {}
  }

  /// Restaure la dernière position de lecture (évite de réinitialiser systématiquement sur Jean)
  Future<Map<String, dynamic>> getLastReadingPosition() async {
    try {
      final prefs = await SharedPreferences.getInstance();
      final book = prefs.getString(_prefBookKey);
      final chapter = prefs.getInt(_prefChapterKey);
      if (book != null && chapter != null) {
        return {'book': book, 'chapter': chapter};
      }
    } catch (_) {}
    return {'book': 'Genesis', 'chapter': 1};
  }

  /// Retourne la liste complète des 66 livres
  Future<List<LocalBibleBook>> getBooks() async {
    if (_cachedBooks != null) return _cachedBooks!;
    await init();

    final booksObj = _bibleData?['books'] as Map<String, dynamic>? ?? {};
    final List<LocalBibleBook> result = [];

    _bookMetadata.forEach((key, meta) {
      final chaptersObj = booksObj[key] as Map<String, dynamic>?;
      final count = chaptersObj?.keys.length ?? 1;

      result.add(
        LocalBibleBook(
          key: key,
          frenchName: meta['fr'] ?? key,
          testament: meta['testament'] ?? 'OT',
          chapterCount: count,
        ),
      );
    });

    _cachedBooks = result;
    return result;
  }

  /// Récupère l'ensemble des versets d'un chapitre donné
  Future<List<LocalBibleVerse>> getVerses(String bookKey, int chapter) async {
    await init();
    final booksObj = _bibleData?['books'] as Map<String, dynamic>? ?? {};
    final bookData = booksObj[bookKey] as Map<String, dynamic>?;
    final chapterData = bookData?[chapter.toString()] as Map<String, dynamic>?;

    if (chapterData == null) {
      // Versets de secours si le chapitre n'est pas encore mappé
      return [
        LocalBibleVerse(number: 1, text: 'Texte biblique en cours de chargement...'),
      ];
    }

    final List<LocalBibleVerse> verses = [];
    final keys = chapterData.keys.map((k) => int.tryParse(k) ?? 0).toList()..sort();

    for (final num in keys) {
      final text = chapterData[num.toString()] as String? ?? '';
      verses.add(LocalBibleVerse(number: num, text: text));
    }

    return verses;
  }

  /// Recherche textuelle dans toute la Bible
  Future<List<Map<String, dynamic>>> search(String query, {int limit = 30}) async {
    if (query.trim().length < 3) return [];
    await init();

    final booksObj = _bibleData?['books'] as Map<String, dynamic>? ?? {};
    final List<Map<String, dynamic>> matches = [];
    final lowerQuery = query.toLowerCase();

    for (final entry in _bookMetadata.entries) {
      final bookKey = entry.key;
      final frName = entry.value['fr'] ?? bookKey;
      final chapters = booksObj[bookKey] as Map<String, dynamic>?;

      if (chapters != null) {
        for (final chEntry in chapters.entries) {
          final chNum = chEntry.key;
          final verses = chEntry.value as Map<String, dynamic>?;

          if (verses != null) {
            for (final vEntry in verses.entries) {
              final vNum = vEntry.key;
              final text = vEntry.value.toString();

              if (text.toLowerCase().contains(lowerQuery)) {
                matches.add({
                  'reference': '$frName $chNum:$vNum',
                  'bookKey': bookKey,
                  'chapter': int.tryParse(chNum) ?? 1,
                  'verse': int.tryParse(vNum) ?? 1,
                  'text': text,
                });

                if (matches.length >= limit) return matches;
              }
            }
          }
        }
      }
    }

    return matches;
  }
}
