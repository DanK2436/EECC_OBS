-- ==============================================================================
-- EECC Ecosystem - Migration 000003 : Configuration Supabase Storage
-- Remplace Google Drive par Supabase Storage pour les Podcasts, Documents & Audios
-- ==============================================================================

-- 1. CRÉATION DES BUCKETS DE STOCKAGE SUPABASE
INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
VALUES 
    (
        'podcasts',
        'podcasts',
        true,
        209715200, -- 200 Mo max par épisode audio
        ARRAY['audio/mpeg', 'audio/mp3', 'audio/wav', 'audio/aac', 'audio/m4a']
    ),
    (
        'documents',
        'documents',
        true,
        52428800,  -- 50 Mo max par document
        ARRAY['application/pdf', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 'text/plain']
    ),
    (
        'raw-media',
        'raw-media',
        false, -- Bucket privé pour les enregistrements bruts non traités
        1073741824, -- 1 Go max
        ARRAY['audio/x-wav', 'audio/wav', 'video/mp4']
    )
ON CONFLICT (id) DO UPDATE SET 
    public = EXCLUDED.public,
    file_size_limit = EXCLUDED.file_size_limit,
    allowed_mime_types = EXCLUDED.allowed_mime_types;

-- 2. AJOUT DES COLONNES SUPABASE STORAGE DANS media_assets
ALTER TABLE public.media_assets 
    ADD COLUMN IF NOT EXISTS storage_bucket TEXT DEFAULT 'podcasts',
    ADD COLUMN IF NOT EXISTS storage_path TEXT,
    ADD COLUMN IF NOT EXISTS public_url TEXT;

-- 3. POLITIQUES RLS SUR storage.objects (Sécurité d'accès aux fichiers)

-- Lecture publique pour les podcasts et documents
DROP POLICY IF EXISTS "Lecture publique des podcasts" ON storage.objects;
CREATE POLICY "Lecture publique des podcasts"
ON storage.objects FOR SELECT
USING (bucket_id = 'podcasts');

DROP POLICY IF EXISTS "Lecture publique des documents" ON storage.objects;
CREATE POLICY "Lecture publique des documents"
ON storage.objects FOR SELECT
USING (bucket_id = 'documents');

-- Lecture du raw-media réservée à l'équipe média (Régisseur, Éditeur, Admin)
DROP POLICY IF EXISTS "Lecture raw-media équipe média" ON storage.objects;
CREATE POLICY "Lecture raw-media équipe média"
ON storage.objects FOR SELECT TO authenticated
USING (
    bucket_id = 'raw-media' AND (
        public.is_producer() OR public.is_editor() OR public.is_admin()
    )
);

-- Upload réservé à l'équipe média (Studio, Éditeurs, Admin)
DROP POLICY IF EXISTS "Upload médias équipe média" ON storage.objects;
CREATE POLICY "Upload médias équipe média"
ON storage.objects FOR INSERT TO authenticated
WITH CHECK (
    bucket_id IN ('podcasts', 'documents', 'raw-media') AND (
        public.is_producer() OR public.is_editor() OR public.is_admin()
    )
);

-- Suppression réservée aux administrateurs et régisseurs
DROP POLICY IF EXISTS "Suppression médias admin" ON storage.objects;
CREATE POLICY "Suppression médias admin"
ON storage.objects FOR DELETE TO authenticated
USING (
    bucket_id IN ('podcasts', 'documents', 'raw-media') AND (
        public.is_producer() OR public.is_admin()
    )
);
