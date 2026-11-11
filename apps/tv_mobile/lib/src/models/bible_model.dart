class BibleBook {
  final int id;
  final String code;
  final String name;
  final String testament; // 'OT' ou 'NT'
  final int chapterCount;

  BibleBook({
    required this.id,
    required this.code,
    required this.name,
    required this.testament,
    required this.chapterCount,
  });

  factory BibleBook.fromJson(Map<String, dynamic> json) {
    return BibleBook(
      id: json['id'] as int,
      code: json['code'] as String,
      name: json['name'] as String,
      testament: json['testament'] as String? ?? 'OT',
      chapterCount: json['chapter_count'] as int? ?? 1,
    );
  }
}

class BibleVerse {
  final int id;
  final String bookCode;
  final int chapter;
  final int verse;
  final String text;

  BibleVerse({
    required this.id,
    required this.bookCode,
    required this.chapter,
    required this.verse,
    required this.text,
  });

  factory BibleVerse.fromJson(Map<String, dynamic> json) {
    return BibleVerse(
      id: json['id'] as int,
      bookCode: json['book_code'] as String,
      chapter: json['chapter'] as int,
      verse: json['verse'] as int,
      text: json['text'] as String,
    );
  }
}
