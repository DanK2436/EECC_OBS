-- ==============================================================================
-- EECC Ecosystem - Migration 000005 : CMS Multimédia Intégré pour EECC TV
-- Gestion des Podcasts, Résumés Audios, Petites Vidéos (Shorts), Résumés Textes,
-- Affiches Événements & Galeries Photos Cultes (jusqu'à 200 images par album).
-- Permet à EECC Studio d'ajouter, modifier, supprimer et masquer (toggle visibilité).
-- ==============================================================================

-- 1. BUCKETS DE STOCKAGE SUPABASE STORAGE
INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
VALUES 
    (
        'podcasts',
        'podcasts',
        true,
        262144000, -- 250 Mo max par podcast
        ARRAY['audio/mpeg', 'audio/mp3', 'audio/wav', 'audio/aac', 'audio/m4a', 'audio/ogg']
    ),
    (
        'audio-summaries',
        'audio-summaries',
        true,
        52428800,  -- 50 Mo max par capsule audio courte
        ARRAY['audio/mpeg', 'audio/mp3', 'audio/wav', 'audio/aac', 'audio/m4a']
    ),
    (
        'short-videos',
        'short-videos',
        true,
        314572800, -- 300 Mo max par vidéo courte/short
        ARRAY['video/mp4', 'video/webm', 'video/quicktime', 'video/x-m4v']
    ),
    (
        'banners',
        'banners',
        true,
        15728640,  -- 15 Mo max par affiche/flyer
        ARRAY['image/jpeg', 'image/png', 'image/webp', 'image/gif']
    ),
    (
        'photo-galleries',
        'photo-galleries',
        true,
        15728640,  -- 15 Mo max par photo de culte
        ARRAY['image/jpeg', 'image/png', 'image/webp']
    )
ON CONFLICT (id) DO UPDATE SET 
    public = EXCLUDED.public,
    file_size_limit = EXCLUDED.file_size_limit,
    allowed_mime_types = EXCLUDED.allowed_mime_types;

-- 2. ENRICHISSEMENT DE LA TABLE SERMONS (PRÉDICATIONS & RÉSUMÉS TEXTES)
ALTER TABLE public.sermons
    ADD COLUMN IF NOT EXISTS is_published BOOLEAN NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS full_transcript TEXT,
    ADD COLUMN IF NOT EXISTS reading_time_minutes INTEGER DEFAULT 5,
    ADD COLUMN IF NOT EXISTS cover_image_url TEXT,
    ADD COLUMN IF NOT EXISTS podcast_url TEXT,
    ADD COLUMN IF NOT EXISTS podcast_duration_seconds INTEGER DEFAULT 0;

-- 3. TABLE DES RÉSUMÉS AUDIOS (CAPSULES SPIRITUELLES & EXTRAITS COURTS)
CREATE TABLE IF NOT EXISTS public.audio_summaries (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sermon_id UUID REFERENCES public.sermons(id) ON DELETE CASCADE,
    title TEXT NOT NULL,
    speaker TEXT NOT NULL,
    date DATE NOT NULL DEFAULT CURRENT_DATE,
    description TEXT,
    audio_url TEXT NOT NULL,
    storage_path TEXT,
    duration_seconds INTEGER NOT NULL DEFAULT 0,
    cover_image_url TEXT,
    is_published BOOLEAN NOT NULL DEFAULT true,
    listen_count INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 4. TABLE DES PETITES VIDÉOS (SHORTS, REELS, CLIPS DE CULTE & TEASERS)
CREATE TABLE IF NOT EXISTS public.short_videos (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sermon_id UUID REFERENCES public.sermons(id) ON DELETE SET NULL,
    title TEXT NOT NULL,
    description TEXT,
    speaker TEXT,
    date DATE NOT NULL DEFAULT CURRENT_DATE,
    video_url TEXT NOT NULL,
    storage_path TEXT,
    thumbnail_url TEXT,
    duration_seconds INTEGER NOT NULL DEFAULT 0,
    aspect_ratio TEXT NOT NULL DEFAULT '9:16', -- '9:16' pour vertical, '16:9' pour horizontal
    is_published BOOLEAN NOT NULL DEFAULT true,
    view_count INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 5. TABLE DES AFFICHES PUBLICITAIRES & ÉVÉNEMENTS (IMAGE UNIQUE / FLYER)
CREATE TABLE IF NOT EXISTS public.event_banners (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id UUID REFERENCES public.events(id) ON DELETE SET NULL,
    title TEXT NOT NULL,
    subtitle TEXT,
    image_url TEXT NOT NULL,
    storage_path TEXT,
    link_url TEXT,
    start_date DATE NOT NULL DEFAULT CURRENT_DATE,
    end_date DATE,
    display_order INTEGER NOT NULL DEFAULT 0,
    is_published BOOLEAN NOT NULL DEFAULT true,
    click_count INTEGER NOT NULL DEFAULT 0,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- 6. TABLES DES GALERIES PHOTOS DE CULTE (JUSQU'À 200 PHOTOS PAR ALBUM)
CREATE TABLE IF NOT EXISTS public.photo_albums (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id UUID REFERENCES public.events(id) ON DELETE SET NULL,
    title TEXT NOT NULL,
    description TEXT,
    date DATE NOT NULL DEFAULT CURRENT_DATE,
    cover_image_url TEXT,
    photo_count INTEGER NOT NULL DEFAULT 0,
    is_published BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS public.album_photos (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    album_id UUID NOT NULL REFERENCES public.photo_albums(id) ON DELETE CASCADE,
    photo_url TEXT NOT NULL,
    thumbnail_url TEXT,
    storage_path TEXT,
    caption TEXT,
    display_order INTEGER NOT NULL DEFAULT 0,
    width INTEGER DEFAULT 1920,
    height INTEGER DEFAULT 1080,
    file_size_bytes BIGINT DEFAULT 0,
    is_published BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Index pour performance optimale sur mobile
CREATE INDEX IF NOT EXISTS idx_audio_summaries_published ON public.audio_summaries(is_published, date DESC);
CREATE INDEX IF NOT EXISTS idx_short_videos_published ON public.short_videos(is_published, date DESC);
CREATE INDEX IF NOT EXISTS idx_event_banners_published ON public.event_banners(is_published, display_order ASC);
CREATE INDEX IF NOT EXISTS idx_photo_albums_published ON public.photo_albums(is_published, date DESC);
CREATE INDEX IF NOT EXISTS idx_album_photos_album ON public.album_photos(album_id, display_order ASC) WHERE is_published = true;
CREATE INDEX IF NOT EXISTS idx_sermons_published ON public.sermons(is_published, date DESC);

-- Trigger pour mettre à jour automatiquement photo_count dans photo_albums
CREATE OR REPLACE FUNCTION public.update_album_photo_count()
RETURNS TRIGGER AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        UPDATE public.photo_albums
        SET photo_count = photo_count + 1, updated_at = NOW()
        WHERE id = NEW.album_id;
    ELSIF TG_OP = 'DELETE' THEN
        UPDATE public.photo_albums
        SET photo_count = GREATEST(0, photo_count - 1), updated_at = NOW()
        WHERE id = OLD.album_id;
    END IF;
    RETURN NULL;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_update_photo_count ON public.album_photos;
CREATE TRIGGER trg_update_photo_count
AFTER INSERT OR DELETE ON public.album_photos
FOR EACH ROW EXECUTE FUNCTION public.update_album_photo_count();

-- 7. POLITIQUES DE SÉCURITÉ ROW LEVEL SECURITY (RLS)

ALTER TABLE public.audio_summaries ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.short_videos ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.event_banners ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.photo_albums ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.album_photos ENABLE ROW LEVEL SECURITY;

-- Lecture publique : Uniquement les éléments non masqués (is_published = true)
DROP POLICY IF EXISTS "Lecture publique audio_summaries" ON public.audio_summaries;
CREATE POLICY "Lecture publique audio_summaries" ON public.audio_summaries FOR SELECT
USING (is_published = true);

DROP POLICY IF EXISTS "Lecture publique short_videos" ON public.short_videos;
CREATE POLICY "Lecture publique short_videos" ON public.short_videos FOR SELECT
USING (is_published = true);

DROP POLICY IF EXISTS "Lecture publique event_banners" ON public.event_banners;
CREATE POLICY "Lecture publique event_banners" ON public.event_banners FOR SELECT
USING (is_published = true);

DROP POLICY IF EXISTS "Lecture publique photo_albums" ON public.photo_albums;
CREATE POLICY "Lecture publique photo_albums" ON public.photo_albums FOR SELECT
USING (is_published = true);

DROP POLICY IF EXISTS "Lecture publique album_photos" ON public.album_photos;
CREATE POLICY "Lecture publique album_photos" ON public.album_photos FOR SELECT
USING (is_published = true);

-- Écriture, Modification & Suppression (Studio Native / Équipe Média / Admin)
DROP POLICY IF EXISTS "Gestion audio_summaries studio" ON public.audio_summaries;
CREATE POLICY "Gestion audio_summaries studio" ON public.audio_summaries FOR ALL TO authenticated
USING (public.is_producer() OR public.is_editor() OR public.is_admin())
WITH CHECK (public.is_producer() OR public.is_editor() OR public.is_admin());

DROP POLICY IF EXISTS "Gestion short_videos studio" ON public.short_videos;
CREATE POLICY "Gestion short_videos studio" ON public.short_videos FOR ALL TO authenticated
USING (public.is_producer() OR public.is_editor() OR public.is_admin())
WITH CHECK (public.is_producer() OR public.is_editor() OR public.is_admin());

DROP POLICY IF EXISTS "Gestion event_banners studio" ON public.event_banners;
CREATE POLICY "Gestion event_banners studio" ON public.event_banners FOR ALL TO authenticated
USING (public.is_producer() OR public.is_editor() OR public.is_admin())
WITH CHECK (public.is_producer() OR public.is_editor() OR public.is_admin());

DROP POLICY IF EXISTS "Gestion photo_albums studio" ON public.photo_albums;
CREATE POLICY "Gestion photo_albums studio" ON public.photo_albums FOR ALL TO authenticated
USING (public.is_producer() OR public.is_editor() OR public.is_admin())
WITH CHECK (public.is_producer() OR public.is_editor() OR public.is_admin());

DROP POLICY IF EXISTS "Gestion album_photos studio" ON public.album_photos;
CREATE POLICY "Gestion album_photos studio" ON public.album_photos FOR ALL TO authenticated
USING (public.is_producer() OR public.is_editor() OR public.is_admin())
WITH CHECK (public.is_producer() OR public.is_editor() OR public.is_admin());

-- 8. POLITIQUES RLS SUR LES NOUVEAUX BUCKETS STORAGE
DROP POLICY IF EXISTS "Lecture publique storage audio_summaries" ON storage.objects;
CREATE POLICY "Lecture publique storage audio_summaries" ON storage.objects FOR SELECT
USING (bucket_id = 'audio-summaries');

DROP POLICY IF EXISTS "Lecture publique storage short_videos" ON storage.objects;
CREATE POLICY "Lecture publique storage short_videos" ON storage.objects FOR SELECT
USING (bucket_id = 'short-videos');

DROP POLICY IF EXISTS "Lecture publique storage banners" ON storage.objects;
CREATE POLICY "Lecture publique storage banners" ON storage.objects FOR SELECT
USING (bucket_id = 'banners');

DROP POLICY IF EXISTS "Lecture publique storage photo_galleries" ON storage.objects;
CREATE POLICY "Lecture publique storage photo_galleries" ON storage.objects FOR SELECT
USING (bucket_id = 'photo-galleries');

-- Upload & Purge réservés aux producteurs/régisseurs/admin
DROP POLICY IF EXISTS "Upload storage media CMS studio" ON storage.objects;
CREATE POLICY "Upload storage media CMS studio" ON storage.objects FOR INSERT TO authenticated
WITH CHECK (
    bucket_id IN ('podcasts', 'audio-summaries', 'short-videos', 'banners', 'photo-galleries') AND
    (public.is_producer() OR public.is_editor() OR public.is_admin())
);

DROP POLICY IF EXISTS "Delete storage media CMS studio" ON storage.objects;
CREATE POLICY "Delete storage media CMS studio" ON storage.objects FOR DELETE TO authenticated
USING (
    bucket_id IN ('podcasts', 'audio-summaries', 'short-videos', 'banners', 'photo-galleries') AND
    (public.is_producer() OR public.is_admin())
);
