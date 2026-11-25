-- ==============================================================================
-- EECC Ecosystem - Migration 000004 : Médiathèque Adaptative, Chapitres & Synchronisation (Jellyfin-like)
-- ==============================================================================

-- 1. TABLE DE REPRISE DE LECTURE MULTI-ÉCRANS (playback_progress)
CREATE TABLE IF NOT EXISTS public.playback_progress (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES auth.users(id) ON DELETE CASCADE,
    media_id UUID NOT NULL REFERENCES public.media_assets(id) ON DELETE CASCADE,
    position_seconds INTEGER NOT NULL DEFAULT 0,
    duration_seconds INTEGER NOT NULL DEFAULT 0,
    completed BOOLEAN NOT NULL DEFAULT false,
    device_name TEXT,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT timezone('utc'::text, now()),
    UNIQUE (user_id, media_id)
);

-- Index pour requêtes ultra-rapides lors du chargement de l'app TV ou du site web
CREATE INDEX IF NOT EXISTS idx_playback_progress_user ON public.playback_progress(user_id);
CREATE INDEX IF NOT EXISTS idx_playback_progress_media ON public.playback_progress(media_id);

-- RLS sur playback_progress : chaque fidèle ne peut lire et modifier que sa propre progression
ALTER TABLE public.playback_progress ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "Utilisateurs gèrent leur progression de lecture" ON public.playback_progress;
CREATE POLICY "Utilisateurs gèrent leur progression de lecture"
ON public.playback_progress FOR ALL TO authenticated
USING (auth.uid() = user_id)
WITH CHECK (auth.uid() = user_id);

-- 2. TABLE DU CHAPITRAGE DES CULTES ET ÉVÉNEMENTS (service_chapters)
CREATE TABLE IF NOT EXISTS public.service_chapters (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    media_id UUID REFERENCES public.media_assets(id) ON DELETE CASCADE,
    event_id UUID REFERENCES public.events(id) ON DELETE CASCADE,
    title TEXT NOT NULL,
    category TEXT NOT NULL CHECK (category IN ('louange', 'priere', 'lecture_biblique', 'predication', 'offrande', 'annonces', 'sainte_cene', 'benediction')),
    start_seconds INTEGER NOT NULL,
    end_seconds INTEGER,
    created_at TIMESTAMPTZ NOT NULL DEFAULT timezone('utc'::text, now())
);

CREATE INDEX IF NOT EXISTS idx_service_chapters_media ON public.service_chapters(media_id);
CREATE INDEX IF NOT EXISTS idx_service_chapters_event ON public.service_chapters(event_id);

ALTER TABLE public.service_chapters ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS "Lecture publique des chapitres" ON public.service_chapters;
CREATE POLICY "Lecture publique des chapitres"
ON public.service_chapters FOR SELECT
USING (true);

DROP POLICY IF EXISTS "Édition des chapitres par l'équipe média" ON public.service_chapters;
CREATE POLICY "Édition des chapitres par l'équipe média"
ON public.service_chapters FOR ALL TO authenticated
USING (public.is_producer() OR public.is_editor() OR public.is_admin())
WITH CHECK (public.is_producer() OR public.is_editor() OR public.is_admin());

-- 3. AJOUT DES CHAMPS TRICKPLAY, HLS ET SOUS-TITRES DANS media_assets
ALTER TABLE public.media_assets
    ADD COLUMN IF NOT EXISTS hls_master_url TEXT,
    ADD COLUMN IF NOT EXISTS trickplay_vtt_url TEXT,
    ADD COLUMN IF NOT EXISTS subtitles_vtt_url TEXT,
    ADD COLUMN IF NOT EXISTS available_resolutions TEXT[] DEFAULT ARRAY['1080p', '720p', '480p', '360p'];
