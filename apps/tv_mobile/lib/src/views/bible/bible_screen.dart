import 'package:flutter/material.dart';
import '../../constants/colors.dart';
import '../../services/bible_local_service.dart';

class BibleScreen extends StatefulWidget {
  const BibleScreen({super.key});

  @override
  State<BibleScreen> createState() => _BibleScreenState();
}

class _BibleScreenState extends State<BibleScreen> {
  final BibleLocalService _bibleService = BibleLocalService();
  List<LocalBibleBook> _books = [];
  LocalBibleBook? _selectedBook;
  int _selectedChapter = 1;
  List<LocalBibleVerse> _verses = [];
  bool _isLoading = true;
  double _fontSize = 16.0;

  @override
  void initState() {
    super.initState();
    _loadInitialData();
  }

  Future<void> _loadInitialData() async {
    final books = await _bibleService.getBooks();
    final lastPosition = await _bibleService.getLastReadingPosition();
    final savedBookKey = lastPosition['book'] as String? ?? 'Genesis';
    final savedChapter = lastPosition['chapter'] as int? ?? 1;

    if (mounted) {
      setState(() {
        _books = books;
        _selectedBook = books.firstWhere(
          (b) => b.key == savedBookKey,
          orElse: () => books.first,
        );
        _selectedChapter = savedChapter.clamp(1, _selectedBook?.chapterCount ?? 1);
      });
      _loadVerses();
    }
  }

  Future<void> _loadVerses() async {
    if (_selectedBook == null) return;
    final verses = await _bibleService.getVerses(_selectedBook!.key, _selectedChapter);
    await _bibleService.saveReadingPosition(_selectedBook!.key, _selectedChapter);
    if (mounted) {
      setState(() {
        _verses = verses;
        _isLoading = false;
      });
    }
  }

  void _openSearchDialog() {
    showDialog(
      context: context,
      builder: (context) => _BibleSearchDialog(
        onSelectResult: (bookKey, chapter) {
          final book = _books.firstWhere((b) => b.key == bookKey, orElse: () => _books.first);
          setState(() {
            _selectedBook = book;
            _selectedChapter = chapter;
            _isLoading = true;
          });
          _loadVerses();
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: EeccColors.background,
      appBar: AppBar(
        backgroundColor: EeccColors.surface,
        elevation: 0,
        title: Row(
          children: [
            const Icon(Icons.menu_book_rounded, color: EeccColors.primary),
            const SizedBox(width: 10),
            Text(
              _selectedBook != null
                  ? '${_selectedBook!.frenchName} $_selectedChapter'
                  : 'Sainte Bible (LSG)',
              style: const TextStyle(fontWeight: FontWeight.bold, color: EeccColors.textMain),
            ),
          ],
        ),
        actions: [
          IconButton(
            icon: const Icon(Icons.search_rounded, color: EeccColors.slate),
            tooltip: 'Recherche dans la Bible',
            onPressed: _openSearchDialog,
          ),
          IconButton(
            icon: const Icon(Icons.format_size_rounded, color: EeccColors.slate),
            tooltip: 'Ajuster la taille du texte',
            onPressed: () {
              setState(() {
                _fontSize = _fontSize == 16.0 ? 19.0 : (_fontSize == 19.0 ? 22.0 : 16.0);
              });
            },
          ),
        ],
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator(color: EeccColors.primary))
          : Column(
              children: [
                // Barre de Sélection Rapide Livre & Chapitre
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                  color: EeccColors.surface,
                  child: Row(
                    children: [
                      // Sélecteur de Livre
                      Expanded(
                        child: DropdownButtonHideUnderline(
                          child: DropdownButton<LocalBibleBook>(
                            value: _selectedBook,
                            isExpanded: true,
                            items: _books.map((b) {
                              return DropdownMenuItem(
                                value: b,
                                child: Text(
                                  b.frenchName,
                                  style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 15),
                                ),
                              );
                            }).toList(),
                            onChanged: (newBook) {
                              if (newBook != null) {
                                setState(() {
                                  _selectedBook = newBook;
                                  _selectedChapter = 1;
                                  _isLoading = true;
                                });
                                _loadVerses();
                              }
                            },
                          ),
                        ),
                      ),
                      const SizedBox(width: 12),
                      // Sélecteur de Chapitre
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 10),
                        decoration: BoxDecoration(
                          color: EeccColors.background,
                          borderRadius: BorderRadius.circular(8),
                          border: Border.all(color: EeccColors.border),
                        ),
                        child: DropdownButtonHideUnderline(
                          child: DropdownButton<int>(
                            value: _selectedChapter,
                            items: List.generate(
                              _selectedBook?.chapterCount ?? 1,
                              (i) => i + 1,
                            ).map((ch) {
                              return DropdownMenuItem(
                                value: ch,
                                child: Text('Ch. $ch', style: const TextStyle(fontWeight: FontWeight.bold)),
                              );
                            }).toList(),
                            onChanged: (newCh) {
                              if (newCh != null) {
                                setState(() {
                                  _selectedChapter = newCh;
                                  _isLoading = true;
                                });
                                _loadVerses();
                              }
                            },
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
                const Divider(height: 1, color: EeccColors.border),

                // Texte Biblique Intégral
                Expanded(
                  child: ListView.builder(
                    padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 16),
                    itemCount: _verses.length,
                    itemBuilder: (context, index) {
                      final verse = _verses[index];
                      return Padding(
                        padding: const EdgeInsets.only(bottom: 14),
                        child: RichText(
                          text: TextSpan(
                            children: [
                              TextSpan(
                                text: '${verse.number} ',
                                style: TextStyle(
                                  fontSize: _fontSize - 2,
                                  fontWeight: FontWeight.bold,
                                  color: EeccColors.primary,
                                ),
                              ),
                              TextSpan(
                                text: verse.text,
                                style: TextStyle(
                                  fontSize: _fontSize,
                                  height: 1.65,
                                  color: EeccColors.textMain,
                                ),
                              ),
                            ],
                          ),
                        ),
                      );
                    },
                  ),
                ),
              ],
            ),
    );
  }
}

class _BibleSearchDialog extends StatefulWidget {
  final Function(String bookKey, int chapter) onSelectResult;

  const _BibleSearchDialog({required this.onSelectResult});

  @override
  State<_BibleSearchDialog> createState() => _BibleSearchDialogState();
}

class _BibleSearchDialogState extends State<_BibleSearchDialog> {
  final TextEditingController _searchController = TextEditingController();
  final BibleLocalService _service = BibleLocalService();
  List<Map<String, dynamic>> _results = [];
  bool _searching = false;

  void _performSearch(String query) async {
    if (query.trim().length < 3) return;
    setState(() => _searching = true);
    final list = await _service.search(query.trim());
    if (mounted) {
      setState(() {
        _results = list;
        _searching = false;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Dialog(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
      child: Container(
        padding: const EdgeInsets.all(16),
        height: 480,
        child: Column(
          children: [
            Row(
              children: [
                const Icon(Icons.search_rounded, color: EeccColors.primary),
                const SizedBox(width: 8),
                const Text('Rechercher dans la Bible', style: TextStyle(fontWeight: FontWeight.bold)),
                const Spacer(),
                IconButton(
                  icon: const Icon(Icons.close_rounded),
                  onPressed: () => Navigator.pop(context),
                ),
              ],
            ),
            TextField(
              controller: _searchController,
              autofocus: true,
              decoration: const InputDecoration(
                hintText: 'Mot-clé (ex: foi, amour, grâce...)',
                border: OutlineInputBorder(),
              ),
              onSubmitted: _performSearch,
            ),
            const SizedBox(height: 10),
            if (_searching)
              const Center(child: Padding(padding: EdgeInsets.all(16), child: CircularProgressIndicator()))
            else
              Expanded(
                child: _results.isEmpty
                  ? const Center(child: Text('Entrez au moins 3 lettres pour chercher.', style: TextStyle(color: EeccColors.textSecondary)))
                  : ListView.builder(
                      itemCount: _results.length,
                      itemBuilder: (context, index) {
                        final item = _results[index];
                        return ListTile(
                          title: Text(item['reference'] as String, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13, color: EeccColors.primary)),
                          subtitle: Text(item['text'] as String, maxLines: 2, overflow: TextOverflow.ellipsis, style: const TextStyle(fontSize: 12)),
                          onTap: () {
                            Navigator.pop(context);
                            widget.onSelectResult(item['bookKey'] as String, item['chapter'] as int);
                          },
                        );
                      },
                    ),
              ),
          ],
        ),
      ),
    );
  }
}
