import 'package:flutter/material.dart';
import '../../constants/colors.dart';
import '../../services/supabase_cms_service.dart';
import '../live/fullscreen_player.dart';
import 'published_image.dart';

class PostersPanel extends StatefulWidget {
  const PostersPanel({super.key});

  @override
  State<PostersPanel> createState() => _PostersPanelState();
}

class _PostersPanelState extends State<PostersPanel> {
  final SupabaseCmsService _cms = SupabaseCmsService();
  List<CmsEventBanner> _banners = [];
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final banners = await _cms.getActiveBanners();
    if (!mounted) return;
    setState(() {
      _banners = banners.where((banner) => banner.imageUrl.trim().isNotEmpty).toList();
      _loading = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator(color: EeccColors.primary));
    }
    if (_banners.isEmpty) {
      return const Center(child: Text('Aucune affiche publiée.', style: TextStyle(color: EeccColors.textSecondary)));
    }
    return RefreshIndicator(
      onRefresh: _load,
      color: EeccColors.primary,
      child: ListView.separated(
        padding: const EdgeInsets.all(16),
        itemCount: _banners.length,
        separatorBuilder: (_, __) => const SizedBox(height: 14),
        itemBuilder: (context, index) {
          final banner = _banners[index];
          return GestureDetector(
            onTap: () => openImageViewer(context, banner.imageUrl, banner.title),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(12),
                  child: AspectRatio(
                    aspectRatio: 16 / 9,
                    child: PublishedImage(url: banner.imageUrl),
                  ),
                ),
                const SizedBox(height: 8),
                Text(banner.title, style: const TextStyle(fontWeight: FontWeight.bold, color: EeccColors.textMain)),
                if ((banner.subtitle ?? '').isNotEmpty)
                  Text(banner.subtitle!, style: const TextStyle(color: EeccColors.textSecondary, fontSize: 12)),
              ],
            ),
          );
        },
      ),
    );
  }
}

class AlbumsPanel extends StatefulWidget {
  const AlbumsPanel({super.key});

  @override
  State<AlbumsPanel> createState() => _AlbumsPanelState();
}

class _AlbumsPanelState extends State<AlbumsPanel> {
  final SupabaseCmsService _cms = SupabaseCmsService();
  List<CmsPhotoAlbum> _albums = [];
  List<CmsShortVideo> _clips = [];
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final albums = await _cms.getPublishedPhotoAlbums();
    final clips = await _cms.getPublishedShortVideos();
    if (!mounted) return;
    setState(() {
      _albums = albums;
      _clips = clips;
      _loading = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Center(child: CircularProgressIndicator(color: EeccColors.primary));
    }
    if (_albums.isEmpty && _clips.isEmpty) {
      return const Center(child: Text('Aucune photo ni clip publié.', style: TextStyle(color: EeccColors.textSecondary)));
    }
    return RefreshIndicator(
      onRefresh: _load,
      color: EeccColors.primary,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          if (_clips.isNotEmpty) ...[
            const Text('Clips', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: EeccColors.textMain)),
            const SizedBox(height: 10),
            ..._clips.map(_clip),
            const SizedBox(height: 18),
          ],
          if (_albums.isNotEmpty)
            const Text('Galeries', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 16, color: EeccColors.textMain)),
          const SizedBox(height: 10),
          ..._albums.map(_album),
        ],
      ),
    );
  }

  Widget _clip(CmsShortVideo clip) {
    final vertical = clip.aspectRatio.contains('9:16') || clip.aspectRatio.contains('9/16');
    final image = (clip.thumbnailUrl ?? '').trim();
    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: SizedBox(
        width: 72,
        height: 72,
        child: ClipRRect(
          borderRadius: BorderRadius.circular(8),
          child: image.isEmpty
              ? const ColoredBox(color: Color(0xFF1C2128), child: Icon(Icons.movie, color: Colors.white70))
              : PublishedImage(url: image),
        ),
      ),
      title: Text(clip.title, style: const TextStyle(color: EeccColors.textMain, fontWeight: FontWeight.w600)),
      subtitle: Text(clip.speaker ?? 'EECC', style: const TextStyle(color: EeccColors.textSecondary)),
      onTap: clip.videoUrl.trim().isEmpty
          ? null
          : () => openProgramFullscreen(context, videoUrl: clip.videoUrl, vertical: vertical),
    );
  }

  Widget _album(CmsPhotoAlbum album) {
    final cover = (album.coverImageUrl ?? '').trim();
    return GestureDetector(
      onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => AlbumPage(album: album))),
      child: Container(
        margin: const EdgeInsets.only(bottom: 14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            ClipRRect(
              borderRadius: BorderRadius.circular(12),
              child: AspectRatio(
                aspectRatio: 16 / 9,
                child: cover.isEmpty
                    ? const ColoredBox(color: Color(0xFF1C2128), child: Icon(Icons.photo_library, color: Colors.white70))
                    : PublishedImage(url: cover),
              ),
            ),
            const SizedBox(height: 8),
            Text(album.title, style: const TextStyle(fontWeight: FontWeight.bold, color: EeccColors.textMain)),
            Text('${album.photoCount} photo(s)', style: const TextStyle(color: EeccColors.textSecondary, fontSize: 12)),
          ],
        ),
      ),
    );
  }
}

class AlbumPage extends StatefulWidget {
  const AlbumPage({super.key, required this.album});

  final CmsPhotoAlbum album;

  @override
  State<AlbumPage> createState() => _AlbumPageState();
}

class _AlbumPageState extends State<AlbumPage> {
  final SupabaseCmsService _cms = SupabaseCmsService();
  List<CmsAlbumPhoto> _photos = [];
  bool _loading = true;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    final photos = await _cms.getAlbumPhotos(widget.album.id);
    if (!mounted) return;
    setState(() {
      _photos = photos.where((photo) => photo.photoUrl.trim().isNotEmpty).toList();
      _loading = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: EeccColors.background,
      appBar: AppBar(title: Text(widget.album.title)),
      body: _loading
          ? const Center(child: CircularProgressIndicator(color: EeccColors.primary))
          : _photos.isEmpty
              ? const Center(child: Text('Aucune photo publiée dans cet album.', style: TextStyle(color: EeccColors.textSecondary)))
              : GridView.builder(
                  padding: const EdgeInsets.all(12),
                  gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                    crossAxisCount: 2,
                    mainAxisSpacing: 10,
                    crossAxisSpacing: 10,
                  ),
                  itemCount: _photos.length,
                  itemBuilder: (context, index) {
                    final photo = _photos[index];
                    final shown = (photo.thumbnailUrl ?? '').trim().isNotEmpty ? photo.thumbnailUrl! : photo.photoUrl;
                    return GestureDetector(
                      onTap: () => openImageViewer(context, photo.photoUrl, photo.caption ?? widget.album.title),
                      child: ClipRRect(
                        borderRadius: BorderRadius.circular(10),
                        child: PublishedImage(url: shown),
                      ),
                    );
                  },
                ),
    );
  }
}
