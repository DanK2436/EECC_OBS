-- ==============================================================================
-- EECC Ecosystem - Migration 000000 : Schéma Initial
-- Conforme aux spécifications fonctionnelles et techniques v1.0
-- ==============================================================================

-- 1. EXTENSIONS REQUISES
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- 2. TYPES ET ÉNUMÉRATIONS
CREATE TYPE app_role AS ENUM (
    'visitor',      -- Visiteur public
    'fidele',       -- Membre fidèle connecté
    'editor',       -- Éditeur de contenu (actualités, résumés, podcasts)
    'producer',     -- Régisseur (gestion des directs, scènes, studio)
    'treasurer',    -- Trésorier (gestion des dons, rapports, retraits)
    'admin'         -- Administrateur général
);

CREATE TYPE event_status AS ENUM (
    'draft',
    'scheduled',
    'live',
    'ended',
    'archived'
);

CREATE TYPE media_type AS ENUM (
    'audio_raw',
    'podcast',
    'video_cut',
    'document_pdf',
    'document_docx',
    'document_txt'
);

CREATE TYPE media_status AS ENUM (
    'uploading',
    'processing',
    'ready',
    'failed',
    'archived'
);

CREATE TYPE replay_platform AS ENUM (
    'youtube',
    'facebook',
    'tiktok',
    'vimeo',
    'other'
);

CREATE TYPE replay_availability AS ENUM (
    'available',
    'unverified',
    'unavailable',
    'deleted'
);

CREATE TYPE donation_status AS ENUM (
    'pending',
    'confirmed',
    'failed',
    'refunded'
);

CREATE TYPE payment_method AS ENUM (
    'mobile_money',
    'card',
    'bank_transfer',
    'cash',
    'other'
);

CREATE TYPE withdrawal_status AS ENUM (
    'draft',
    'pending_otp',
    'otp_verified',
    'otp_expired',
    'cancelled',
    'locked',
    'processing',
    'completed',
    'failed',
    'pending'
);

-- 3. TABLES UTILISATEURS ET RÔLES
CREATE TABLE profiles (
    id UUID PRIMARY KEY REFERENCES auth.users(id) ON DELETE CASCADE,
    display_name TEXT,
    email TEXT UNIQUE NOT NULL,
    phone TEXT,
    avatar_url TEXT,
    locale TEXT DEFAULT 'fr',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE user_roles (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES profiles(id) ON DELETE CASCADE,
    role app_role NOT NULL DEFAULT 'fidele',
    assigned_at TIMESTAMPTZ DEFAULT NOW(),
    assigned_by UUID REFERENCES profiles(id) ON DELETE SET NULL,
    UNIQUE (user_id, role)
);

-- 4. ÉGLISES ET IMPLANTATIONS
CREATE TABLE churches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name TEXT NOT NULL,
    description TEXT,
    address TEXT NOT NULL,
    city TEXT NOT NULL,
    country TEXT DEFAULT 'RDC',
    phone TEXT,
    email TEXT,
    schedules JSONB DEFAULT '[]'::jsonb, -- Ex: [{"day": "Dimanche", "time": "09:00", "label": "Culte Principal"}]
    latitude DOUBLE PRECISION,
    longitude DOUBLE PRECISION,
    is_main_campus BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 5. CULTES ET ÉVÉNEMENTS
CREATE TABLE events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    church_id UUID REFERENCES churches(id) ON DELETE SET NULL,
    title TEXT NOT NULL,
    description TEXT,
    start_time TIMESTAMPTZ NOT NULL,
    end_time TIMESTAMPTZ,
    event_type TEXT DEFAULT 'worship_service',
    status event_status DEFAULT 'scheduled',
    live_stream_url TEXT, -- Flux live HLS/RTMP pour visionnage direct dans EECC TV
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 6. PRÉDICATIONS / SERMONS
CREATE TABLE sermons (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id UUID REFERENCES events(id) ON DELETE SET NULL,
    title TEXT NOT NULL,
    speaker TEXT NOT NULL,
    date DATE NOT NULL,
    theme TEXT,
    scripture_refs TEXT, -- Ex: "Jean 3:16, Romains 8:28"
    summary TEXT,
    key_points JSONB DEFAULT '[]'::jsonb,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 7. MÉDIAS ET FICHIERS (Synchronisés avec Google Drive)
CREATE TABLE media_assets (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sermon_id UUID REFERENCES sermons(id) ON DELETE CASCADE,
    title TEXT NOT NULL,
    type media_type NOT NULL,
    drive_file_id TEXT, -- ID du fichier sur Google Drive
    drive_web_url TEXT,
    drive_download_url TEXT,
    mime_type TEXT,
    size_bytes BIGINT,
    duration_seconds INTEGER,
    checksum_sha256 TEXT,
    status media_status DEFAULT 'uploading',
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 8. REPLAYS SUR PLATEFORMES TIERCES (YouTube, Facebook, TikTok)
-- Règle fonctionnelle : EECC TV ne conserve que l'URL/ID externe, pas la vidéo elle-même
CREATE TABLE platform_replays (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    event_id UUID NOT NULL REFERENCES events(id) ON DELETE CASCADE,
    platform replay_platform NOT NULL,
    external_id TEXT, -- ID vidéo sur la plateforme
    url TEXT NOT NULL,
    availability_status replay_availability DEFAULT 'unverified',
    last_checked_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 9. BIBLE (Version Louis Segond ou équivalent)
CREATE TABLE bible_books (
    id INTEGER PRIMARY KEY,
    code TEXT UNIQUE NOT NULL, -- Ex: 'GEN', 'JHN'
    name TEXT NOT NULL,
    testament TEXT CHECK (testament IN ('OT', 'NT')),
    chapter_count INTEGER NOT NULL
);

CREATE TABLE bible_verses (
    id BIGSERIAL PRIMARY KEY,
    book_code TEXT NOT NULL REFERENCES bible_books(code) ON DELETE CASCADE,
    chapter INTEGER NOT NULL,
    verse INTEGER NOT NULL,
    text TEXT NOT NULL,
    version TEXT DEFAULT 'LSG_1910',
    CONSTRAINT unique_verse_version UNIQUE (book_code, chapter, verse, version)
);

CREATE INDEX idx_bible_search ON bible_verses USING gin(to_tsvector('french', text));
CREATE INDEX idx_bible_coords ON bible_verses(book_code, chapter, verse);

-- 10. DONS ET TRANSACTIONS (Maishapay)
CREATE TABLE donations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reference TEXT UNIQUE NOT NULL, -- Référence interne unique générée par le backend
    donor_id UUID REFERENCES profiles(id) ON DELETE SET NULL,
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

CREATE INDEX idx_donations_ref ON donations(reference);
CREATE INDEX idx_donations_status ON donations(status);

-- 11. PARAMÈTRES DE SÉCURITÉ DE TRÉSORERIE (Section 20.2)
-- L'adresse de réception OTP est ajoutée manuellement par l'administrateur dans le logiciel
CREATE TABLE treasury_settings (
    id INTEGER PRIMARY KEY DEFAULT 1 CHECK (id = 1), -- Singleton
    verified_security_email TEXT, -- À renseigner manuellement dans EECC Studio
    email_verification_status TEXT DEFAULT 'pending', -- 'pending', 'verified'
    otp_validity_minutes INTEGER DEFAULT 5,
    max_otp_attempts INTEGER DEFAULT 5,
    updated_by UUID REFERENCES profiles(id),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- 12. DEMANDES DE RETRAIT ET AUTHENTIFICATION OTP (Section 20.3 & 20.5)
CREATE TABLE withdrawal_requests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reference TEXT UNIQUE NOT NULL, -- Ex: 'WDR-20261111-XXXX'
    requested_by UUID NOT NULL REFERENCES profiles(id),
    amount_gross NUMERIC(12, 2) NOT NULL CHECK (amount_gross > 0),
    fee NUMERIC(12, 2) DEFAULT 0.00,
    amount_net NUMERIC(12, 2) NOT NULL CHECK (amount_net > 0),
    currency TEXT DEFAULT 'USD',
    destination_account TEXT NOT NULL, -- Numéro mobile money ou compte masqué
    destination_provider TEXT NOT NULL, -- Ex: 'AirtelMoney', 'M-Pesa', 'OrangeMoney'
    status withdrawal_status DEFAULT 'draft',
    otp_hash TEXT, -- Empreinte cryptographique salée du code à 5 caractères (JAMAIS en clair)
    otp_salt TEXT,
    otp_attempts INTEGER DEFAULT 0,
    otp_expires_at TIMESTAMPTZ,
    otp_verified_at TIMESTAMPTZ,
    provider_reference TEXT,
    failure_reason TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_withdrawals_ref ON withdrawal_requests(reference);
CREATE INDEX idx_withdrawals_status ON withdrawal_requests(status);

-- 13. JOURNAL D'AUDIT SÉCURISÉ (Section 20.6)
CREATE TABLE audit_logs (
    id BIGSERIAL PRIMARY KEY,
    actor_id UUID REFERENCES profiles(id) ON DELETE SET NULL,
    action TEXT NOT NULL, -- Ex: 'WITHDRAWAL_INITIATED', 'OTP_VERIFIED', 'WITHDRAWAL_EXECUTED'
    entity_type TEXT NOT NULL, -- Ex: 'withdrawal_requests', 'donations', 'treasury_settings'
    entity_id TEXT NOT NULL,
    metadata JSONB DEFAULT '{}'::jsonb, -- Ne contient JAMAIS de secret ou de code OTP
    ip_address TEXT,
    user_agent TEXT,
    created_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE INDEX idx_audit_logs_actor ON audit_logs(actor_id);
CREATE INDEX idx_audit_logs_action ON audit_logs(action);
CREATE INDEX idx_audit_logs_entity ON audit_logs(entity_type, entity_id);
