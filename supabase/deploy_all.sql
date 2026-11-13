-- ==============================================================================
-- EECC ECOSYSTEM — SCRIPT DE DÉPLOIEMENT GLOBAL (ALL-IN-ONE)
-- À copier-coller et exécuter en UNE SEULE FOIS dans le SQL Editor de Supabase
-- ==============================================================================

-- ------------------------------------------------------------------------------
-- 1. EXTENSIONS
-- ------------------------------------------------------------------------------
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- ------------------------------------------------------------------------------
-- 2. ÉNUMÉRATIONS ET TYPES
-- ------------------------------------------------------------------------------
DO $$ BEGIN
    CREATE TYPE app_role AS ENUM ('visitor', 'fidele', 'editor', 'producer', 'treasurer', 'admin');
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE TYPE event_status AS ENUM ('draft', 'scheduled', 'live', 'ended', 'archived');
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE TYPE media_type AS ENUM ('audio_raw', 'podcast', 'video_cut', 'document_pdf', 'document_docx', 'document_txt');
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE TYPE media_status AS ENUM ('uploading', 'processing', 'ready', 'failed', 'archived');
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE TYPE replay_platform AS ENUM ('youtube', 'facebook', 'tiktok', 'vimeo', 'other');
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE TYPE replay_availability AS ENUM ('available', 'unverified', 'unavailable', 'deleted');
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE TYPE donation_status AS ENUM ('pending', 'confirmed', 'failed', 'refunded');
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE TYPE payment_method AS ENUM ('mobile_money', 'card', 'bank_transfer', 'cash', 'other');
EXCEPTION WHEN duplicate_object THEN null; END $$;

DO $$ BEGIN
    CREATE TYPE withdrawal_status AS ENUM (
        'draft', 'pending_otp', 'otp_verified', 'otp_expired', 'cancelled', 'locked', 'processing', 'completed', 'failed', 'pending'
    );
EXCEPTION WHEN duplicate_object THEN null; END $$;

-- ------------------------------------------------------------------------------
-- 3. TABLES DU SYSTÈME
-- ------------------------------------------------------------------------------

-- Profils utilisateurs
CREATE TABLE IF NOT EXISTS public.profiles (
    id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    display_name TEXT,
    email TEXT UNIQUE NOT NULL,
    phone TEXT,
    avatar_url TEXT,
    locale TEXT DEFAULT 'fr',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Rôles utilisateurs
CREATE TABLE IF NOT EXISTS public.user_roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES public.profiles(id) ON DELETE CASCADE,
    role app_role NOT NULL DEFAULT 'fidele',
    assigned_at TIMESTAMPTZ DEFAULT NOW(),
    assigned_by UUID REFERENCES public.profiles(id) ON DELETE SET NULL,
    UNIQUE (user_id, role)
);

-- Églises & Implantations
CREATE TABLE IF NOT EXISTS public.churches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    description TEXT,
    address TEXT NOT NULL,
    city TEXT NOT NULL,
    country TEXT DEFAULT 'RDC',
    phone TEXT,
    email TEXT,
    schedules JSONB DEFAULT '[]'::jsonb,
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    is_main_campus BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Événements & Cultes
CREATE TABLE IF NOT EXISTS public.events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    church_id UUID REFERENCES public.churches(id) ON DELETE SET NULL,
    title TEXT NOT NULL,
    description TEXT,
    start_time TIMESTAMPTZ NOT NULL,
    end_time TIMESTAMPTZ,
    event_type TEXT DEFAULT 'worship_service',
    status event_status DEFAULT 'scheduled',
    live_stream_url TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Prédications / Sermons
CREATE TABLE IF NOT EXISTS public.sermons (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id UUID REFERENCES public.events(id) ON DELETE SET NULL,
    title TEXT NOT NULL,
    speaker TEXT NOT NULL,
    date DATE NOT NULL,
    theme TEXT,
    scripture_refs TEXT,
    summary TEXT,
    key_points JSONB DEFAULT '[]'::jsonb,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Médias & Fichiers
CREATE TABLE IF NOT EXISTS public.media_assets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sermon_id UUID REFERENCES public.sermons(id) ON DELETE CASCADE,
    title TEXT NOT NULL,
    type media_type NOT NULL,
    storage_bucket TEXT DEFAULT 'podcasts',
    storage_path TEXT,
    public_url TEXT,
    mime_type TEXT,
    size_bytes BIGINT,
    duration_seconds INTEGER,
    checksum_sha256 TEXT,
    status media_status DEFAULT 'uploading',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Replays Externes (YouTube, Facebook, TikTok)
CREATE TABLE IF NOT EXISTS public.platform_replays (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id UUID NOT NULL REFERENCES public.events(id) ON DELETE CASCADE,
    platform replay_platform NOT NULL,
    external_id TEXT,
    url TEXT NOT NULL,
    availability_status replay_availability DEFAULT 'unverified',
    last_checked_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Bible
CREATE TABLE IF NOT EXISTS public.bible_books (
    id INTEGER PRIMARY KEY,
    code TEXT UNIQUE NOT NULL,
    name TEXT NOT NULL,
    testament TEXT CHECK (testament IN ('OT', 'NT')),
    chapter_count INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS public.bible_verses (
    id BIGSERIAL PRIMARY KEY,
    book_code TEXT NOT NULL REFERENCES public.bible_books(code) ON DELETE CASCADE,
    chapter INTEGER NOT NULL,
    verse INTEGER NOT NULL,
    text TEXT NOT NULL,
    version TEXT DEFAULT 'LSG_1910',
    CONSTRAINT unique_verse_version UNIQUE (book_code, chapter, verse, version)
);

CREATE INDEX IF NOT EXISTS idx_bible_search ON public.bible_verses USING gin(to_tsvector('french', text));
CREATE INDEX IF NOT EXISTS idx_bible_coords ON public.bible_verses(book_code, chapter, verse);

-- Dons et Transactions (Maishapay)
CREATE TABLE IF NOT EXISTS public.donations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reference TEXT UNIQUE NOT NULL,
    donor_id UUID REFERENCES public.profiles(id) ON DELETE SET NULL,
    donor_name TEXT,
    donor_email TEXT,
    donor_phone TEXT,
    amount NUMERIC(12, 2) NOT NULL CHECK (amount > 0),
    currency TEXT DEFAULT 'USD',
    method payment_method DEFAULT 'mobile_money',
    provider TEXT DEFAULT 'maishapay',
    provider_transaction_id TEXT,
    provider_fee NUMERIC(12, 2) DEFAULT 0.00,
    status donation_status DEFAULT 'pending',
    receipt_url TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_donations_ref ON public.donations(reference);
CREATE INDEX IF NOT EXISTS idx_donations_status ON public.donations(status);

-- Paramètres de Sécurité de Trésorerie
-- L'adresse de réception OTP est configurée manuellement dans EECC Studio
CREATE TABLE IF NOT EXISTS public.treasury_settings (
    id INTEGER PRIMARY KEY DEFAULT 1 CHECK (id = 1),
    verified_security_email TEXT,
    email_verification_status TEXT DEFAULT 'pending',
    otp_validity_minutes INTEGER DEFAULT 5,
    max_otp_attempts INTEGER DEFAULT 5,
    updated_by UUID REFERENCES public.profiles(id),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Déverrouiller la contrainte NOT NULL au cas où la table existait déjà auparavant
ALTER TABLE public.treasury_settings 
    ALTER COLUMN verified_security_email DROP NOT NULL;

-- S'assurer que media_assets possède les colonnes Supabase Storage même si créée précédemment
ALTER TABLE public.media_assets 
    ADD COLUMN IF NOT EXISTS storage_bucket TEXT DEFAULT 'podcasts',
    ADD COLUMN IF NOT EXISTS storage_path TEXT,
    ADD COLUMN IF NOT EXISTS public_url TEXT;

-- Demandes de Retrait et Authentification OTP (Section 20)
CREATE TABLE IF NOT EXISTS public.withdrawal_requests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reference TEXT UNIQUE NOT NULL,
    requested_by UUID NOT NULL REFERENCES public.profiles(id),
    amount_gross NUMERIC(12, 2) NOT NULL CHECK (amount_gross > 0),
    fee NUMERIC(12, 2) DEFAULT 0.00,
    amount_net NUMERIC(12, 2) NOT NULL CHECK (amount_net > 0),
    currency TEXT DEFAULT 'USD',
    destination_account TEXT NOT NULL,
    destination_provider TEXT NOT NULL,
    status withdrawal_status DEFAULT 'draft',
    otp_hash TEXT,
    otp_salt TEXT,
    otp_attempts INTEGER DEFAULT 0,
    otp_expires_at TIMESTAMPTZ,
    otp_verified_at TIMESTAMPTZ,
    provider_reference TEXT,
    failure_reason TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_withdrawals_ref ON public.withdrawal_requests(reference);
CREATE INDEX IF NOT EXISTS idx_withdrawals_status ON public.withdrawal_requests(status);

-- Journal d'Audit
CREATE TABLE IF NOT EXISTS public.audit_logs (
    id BIGSERIAL PRIMARY KEY,
    actor_id UUID REFERENCES public.profiles(id) ON DELETE SET NULL,
    action TEXT NOT NULL,
    entity_type TEXT NOT NULL,
    entity_id TEXT NOT NULL,
    metadata JSONB DEFAULT '{}'::jsonb,
    ip_address TEXT,
    user_agent TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- ------------------------------------------------------------------------------
-- 4. FONCTIONS ET TRIGGERS
-- ------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS update_profiles_updated_at ON public.profiles;
CREATE TRIGGER update_profiles_updated_at BEFORE UPDATE ON public.profiles FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_churches_updated_at ON public.churches;
CREATE TRIGGER update_churches_updated_at BEFORE UPDATE ON public.churches FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_events_updated_at ON public.events;
CREATE TRIGGER update_events_updated_at BEFORE UPDATE ON public.events FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_sermons_updated_at ON public.sermons;
CREATE TRIGGER update_sermons_updated_at BEFORE UPDATE ON public.sermons FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_media_assets_updated_at ON public.media_assets;
CREATE TRIGGER update_media_assets_updated_at BEFORE UPDATE ON public.media_assets FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_donations_updated_at ON public.donations;
CREATE TRIGGER update_donations_updated_at BEFORE UPDATE ON public.donations FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_treasury_settings_updated_at ON public.treasury_settings;
CREATE TRIGGER update_treasury_settings_updated_at BEFORE UPDATE ON public.treasury_settings FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_withdrawal_requests_updated_at ON public.withdrawal_requests;
CREATE TRIGGER update_withdrawal_requests_updated_at BEFORE UPDATE ON public.withdrawal_requests FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- Création automatique de profil lors de l'inscription auth
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO public.profiles (id, display_name, email)
    VALUES (
        NEW.id,
        COALESCE(NEW.raw_user_meta_data->>'display_name', split_part(NEW.email, '@', 1)),
        NEW.email
    ) ON CONFLICT (id) DO NOTHING;

    INSERT INTO public.user_roles (user_id, role)
    VALUES (NEW.id, 'fidele')
    ON CONFLICT (user_id, role) DO NOTHING;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

DROP TRIGGER IF EXISTS on_auth_user_created ON auth.users;
CREATE TRIGGER on_auth_user_created AFTER INSERT ON auth.users FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();

-- Journalisation d'audit
CREATE OR REPLACE FUNCTION public.log_audit_event(
    p_actor_id UUID,
    p_action TEXT,
    p_entity_type TEXT,
    p_entity_id TEXT,
    p_metadata JSONB DEFAULT '{}'::jsonb
)
RETURNS VOID AS $$
BEGIN
    INSERT INTO public.audit_logs (actor_id, action, entity_type, entity_id, metadata)
    VALUES (p_actor_id, p_action, p_entity_type, p_entity_id, p_metadata);
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- ------------------------------------------------------------------------------
-- 5. RÔLES ET ROW LEVEL SECURITY (RLS)
-- ------------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.has_role(required_role app_role)
RETURNS BOOLEAN AS $$
BEGIN
    RETURN EXISTS (
        SELECT 1 FROM public.user_roles
        WHERE user_id = auth.uid()
          AND (role = required_role OR role = 'admin')
    );
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE OR REPLACE FUNCTION public.is_admin() RETURNS BOOLEAN AS $$
BEGIN RETURN public.has_role('admin'); END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE OR REPLACE FUNCTION public.is_treasurer() RETURNS BOOLEAN AS $$
BEGIN RETURN public.has_role('treasurer'); END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE OR REPLACE FUNCTION public.is_producer() RETURNS BOOLEAN AS $$
BEGIN RETURN public.has_role('producer'); END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE OR REPLACE FUNCTION public.is_editor() RETURNS BOOLEAN AS $$
BEGIN RETURN public.has_role('editor'); END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- Activation RLS
ALTER TABLE public.profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.user_roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.churches ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.events ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.sermons ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.media_assets ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.platform_replays ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.bible_books ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.bible_verses ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.donations ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.treasury_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.withdrawal_requests ENABLE ROW LEVEL SECURITY;
ALTER TABLE public.audit_logs ENABLE ROW LEVEL SECURITY;

-- Politiques RLS
DROP POLICY IF EXISTS "Profiles visibles publiquement" ON public.profiles;
CREATE POLICY "Profiles visibles publiquement" ON public.profiles FOR SELECT USING (true);

DROP POLICY IF EXISTS "Utilisateurs modifient leur profil" ON public.profiles;
CREATE POLICY "Utilisateurs modifient leur profil" ON public.profiles FOR UPDATE USING (auth.uid() = id);

DROP POLICY IF EXISTS "Churches lecture publique" ON public.churches;
CREATE POLICY "Churches lecture publique" ON public.churches FOR SELECT USING (true);

DROP POLICY IF EXISTS "Events lecture publique" ON public.events;
CREATE POLICY "Events lecture publique" ON public.events FOR SELECT USING (true);

DROP POLICY IF EXISTS "Sermons lecture publique" ON public.sermons;
CREATE POLICY "Sermons lecture publique" ON public.sermons FOR SELECT USING (true);

DROP POLICY IF EXISTS "MediaAssets lecture publique" ON public.media_assets;
CREATE POLICY "MediaAssets lecture publique" ON public.media_assets FOR SELECT USING (status = 'ready');

DROP POLICY IF EXISTS "PlatformReplays lecture publique" ON public.platform_replays;
CREATE POLICY "PlatformReplays lecture publique" ON public.platform_replays FOR SELECT USING (true);

DROP POLICY IF EXISTS "BibleBooks lecture publique" ON public.bible_books;
CREATE POLICY "BibleBooks lecture publique" ON public.bible_books FOR SELECT USING (true);

DROP POLICY IF EXISTS "BibleVerses lecture publique" ON public.bible_verses;
CREATE POLICY "BibleVerses lecture publique" ON public.bible_verses FOR SELECT USING (true);

DROP POLICY IF EXISTS "Dons insertion publique" ON public.donations;
CREATE POLICY "Dons insertion publique" ON public.donations FOR INSERT WITH CHECK (true);

DROP POLICY IF EXISTS "Dons consultation tresorier" ON public.donations;
CREATE POLICY "Dons consultation tresorier" ON public.donations FOR SELECT TO authenticated USING (public.is_treasurer() OR auth.uid() = donor_id);

DROP POLICY IF EXISTS "Parametres tresorerie tresorier" ON public.treasury_settings;
CREATE POLICY "Parametres tresorerie tresorier" ON public.treasury_settings FOR SELECT TO authenticated USING (public.is_treasurer());

DROP POLICY IF EXISTS "Parametres tresorerie modification admin" ON public.treasury_settings;
CREATE POLICY "Parametres tresorerie modification admin" ON public.treasury_settings FOR ALL TO authenticated USING (public.is_admin());

DROP POLICY IF EXISTS "Retraits gestion tresorier" ON public.withdrawal_requests;
CREATE POLICY "Retraits gestion tresorier" ON public.withdrawal_requests FOR ALL TO authenticated USING (public.is_treasurer());

-- ------------------------------------------------------------------------------
-- 6. SUPABASE STORAGE BUCKETS (Podcasts, Documents, Raw-Media)
-- ------------------------------------------------------------------------------
INSERT INTO storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
VALUES 
    ('podcasts', 'podcasts', true, 209715200, ARRAY['audio/mpeg', 'audio/mp3', 'audio/wav', 'audio/aac', 'audio/m4a']),
    ('documents', 'documents', true, 52428800, ARRAY['application/pdf', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 'text/plain']),
    ('raw-media', 'raw-media', false, 1073741824, ARRAY['audio/x-wav', 'audio/wav', 'video/mp4'])
ON CONFLICT (id) DO UPDATE SET 
    public = EXCLUDED.public,
    file_size_limit = EXCLUDED.file_size_limit,
    allowed_mime_types = EXCLUDED.allowed_mime_types;

DROP POLICY IF EXISTS "Lecture publique podcasts" ON storage.objects;
CREATE POLICY "Lecture publique podcasts" ON storage.objects FOR SELECT USING (bucket_id = 'podcasts');

DROP POLICY IF EXISTS "Lecture publique documents" ON storage.objects;
CREATE POLICY "Lecture publique documents" ON storage.objects FOR SELECT USING (bucket_id = 'documents');

DROP POLICY IF EXISTS "Upload médias staff" ON storage.objects;
CREATE POLICY "Upload médias staff" ON storage.objects FOR INSERT TO authenticated
WITH CHECK (bucket_id IN ('podcasts', 'documents', 'raw-media') AND (public.is_producer() OR public.is_editor() OR public.is_admin()));

-- ------------------------------------------------------------------------------
-- 7. INITIALISATION DES DONNÉES (SEED DATA)
-- ------------------------------------------------------------------------------

-- Paramètre initial de trésorerie (en attente de saisie de l'e-mail de réception dans Studio)
INSERT INTO public.treasury_settings (id, verified_security_email, email_verification_status, otp_validity_minutes, max_otp_attempts)
VALUES (1, NULL, 'pending', 5, 5)
ON CONFLICT (id) DO UPDATE SET
    otp_validity_minutes = EXCLUDED.otp_validity_minutes,
    max_otp_attempts = EXCLUDED.max_otp_attempts;

-- Siège principal de l'église
INSERT INTO public.churches (id, name, description, address, city, country, phone, email, is_main_campus, schedules)
VALUES (
    'a0000000-0000-0000-0000-000000000001',
    'Église Évangélique les Cohéritiers du Christ (EECC) — Siège Principal',
    'Communauté vivante d''adoration, d''enseignement et d''impact.',
    'Avenue de la Paix, Quartier Résidentiel',
    'Matadi',
    'RDC',
    '+243 81 000 0000',
    'contact@eecc-eglise.org',
    true,
    '[
        {"day": "Dimanche", "time": "08:30", "label": "Premier Culte"},
        {"day": "Dimanche", "time": "11:00", "label": "Deuxième Culte"},
        {"day": "Mercredi", "time": "17:30", "label": "Étude Biblique & Prière"}
    ]'::jsonb
) ON CONFLICT (id) DO NOTHING;

-- Livres bibliques de base
INSERT INTO public.bible_books (id, code, name, testament, chapter_count) VALUES
    (1, 'GEN', 'Genèse', 'OT', 50),
    (2, 'EXO', 'Exode', 'OT', 40),
    (19, 'PSA', 'Psaumes', 'OT', 150),
    (20, 'PRO', 'Proverbes', 'OT', 31),
    (40, 'MAT', 'Matthieu', 'NT', 28),
    (41, 'MRK', 'Marc', 'NT', 16),
    (42, 'LUK', 'Luc', 'NT', 24),
    (43, 'JHN', 'Jean', 'NT', 21),
    (44, 'ACT', 'Actes des Apôtres', 'NT', 28),
    (45, 'ROM', 'Romains', 'NT', 16),
    (66, 'REV', 'Apocalypse', 'NT', 22)
ON CONFLICT (id) DO NOTHING;

-- Culte de démonstration
INSERT INTO public.events (id, church_id, title, description, start_time, end_time, event_type, status)
VALUES (
    'b0000000-0000-0000-0000-000000000001',
    'a0000000-0000-0000-0000-000000000001',
    'Culte de Célébration & Louange',
    'Venez célébrer la grandeur de Dieu dans la louange et la parole.',
    NOW() + INTERVAL '2 days',
    NOW() + INTERVAL '2 days 2 hours',
    'worship_service',
    'scheduled'
) ON CONFLICT (id) DO NOTHING;
