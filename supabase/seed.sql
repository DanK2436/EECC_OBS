-- ==============================================================================
-- EECC Ecosystem - Données Initiales (Seed Data)
-- ==============================================================================

-- 1. PARAMÈTRE INITIAL DE TRÉSORERIE
-- L'adresse e-mail de réception du code de retrait sera configurée manuellement dans EECC Studio
INSERT INTO public.treasury_settings (
    id,
    verified_security_email,
    email_verification_status,
    otp_validity_minutes,
    max_otp_attempts
) VALUES (
    1,
    NULL,
    'pending',
    5,
    5
) ON CONFLICT (id) DO NOTHING;

-- 2. IMPLANTATION PRINCIPALE DE L'ÉGLISE
INSERT INTO public.churches (
    id,
    name,
    description,
    address,
    city,
    country,
    phone,
    email,
    is_main_campus,
    schedules
) VALUES (
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
) ON CONFLICT DO NOTHING;

-- 3. LIVRES DE LA BIBLE (Exemples clés de l'Ancien et Nouveau Testament)
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

-- 4. ÉVÉNEMENT / CULTE DE DÉMONSTRATION
INSERT INTO public.events (
    id,
    church_id,
    title,
    description,
    start_time,
    end_time,
    event_type,
    status
) VALUES (
    'b0000000-0000-0000-0000-000000000001',
    'a0000000-0000-0000-0000-000000000001',
    'Culte de Célébration & Louange',
    'Venez célébrer la grandeur de Dieu dans la louange et la parole.',
    NOW() + INTERVAL '2 days',
    NOW() + INTERVAL '2 days 2 hours',
    'worship_service',
    'scheduled'
) ON CONFLICT DO NOTHING;
