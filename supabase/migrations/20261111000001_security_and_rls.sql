-- ==============================================================================
-- EECC Ecosystem - Migration 000001 : Sécurité et Politiques RLS (Row Level Security)
-- ==============================================================================

-- 1. FONCTIONS D'AIDE POUR L'ÉVALUATION DES RÔLES
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

CREATE OR REPLACE FUNCTION public.is_admin()
RETURNS BOOLEAN AS $$
BEGIN
    RETURN public.has_role('admin');
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE OR REPLACE FUNCTION public.is_treasurer()
RETURNS BOOLEAN AS $$
BEGIN
    RETURN public.has_role('treasurer');
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE OR REPLACE FUNCTION public.is_producer()
RETURNS BOOLEAN AS $$
BEGIN
    RETURN public.has_role('producer');
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE OR REPLACE FUNCTION public.is_editor()
RETURNS BOOLEAN AS $$
BEGIN
    RETURN public.has_role('editor');
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- 2. ACTIVATION DE LA RLS SUR TOUTES LES TABLES
ALTER TABLE profiles ENABLE ROW LEVEL SECURITY;
ALTER TABLE user_roles ENABLE ROW LEVEL SECURITY;
ALTER TABLE churches ENABLE ROW LEVEL SECURITY;
ALTER TABLE events ENABLE ROW LEVEL SECURITY;
ALTER TABLE sermons ENABLE ROW LEVEL SECURITY;
ALTER TABLE media_assets ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform_replays ENABLE ROW LEVEL SECURITY;
ALTER TABLE bible_books ENABLE ROW LEVEL SECURITY;
ALTER TABLE bible_verses ENABLE ROW LEVEL SECURITY;
ALTER TABLE donations ENABLE ROW LEVEL SECURITY;
ALTER TABLE treasury_settings ENABLE ROW LEVEL SECURITY;
ALTER TABLE withdrawal_requests ENABLE ROW LEVEL SECURITY;
ALTER TABLE audit_logs ENABLE ROW LEVEL SECURITY;

-- 3. POLITIQUES RLS : PROFILES & USER_ROLES
CREATE POLICY "Profiles visibles publiquement"
ON profiles FOR SELECT USING (true);

CREATE POLICY "Utilisateurs peuvent modifier leur propre profil"
ON profiles FOR UPDATE USING (auth.uid() = id);

CREATE POLICY "Admin peut gérer tous les profils"
ON profiles FOR ALL USING (public.is_admin());

CREATE POLICY "Rôles visibles par les utilisateurs authentifiés"
ON user_roles FOR SELECT TO authenticated USING (true);

CREATE POLICY "Seul l'admin peut modifier les rôles"
ON user_roles FOR ALL USING (public.is_admin());

-- 4. POLITIQUES RLS : CONTENUS PUBLICS (Églises, Événements, Sermons, Médias, Replays, Bible)
CREATE POLICY "Churches lecture publique"
ON churches FOR SELECT USING (true);

CREATE POLICY "Churches gestion admin et regisseur"
ON churches FOR ALL USING (public.is_producer());

CREATE POLICY "Events lecture publique"
ON events FOR SELECT USING (true);

CREATE POLICY "Events gestion admin et regisseur"
ON events FOR ALL USING (public.is_producer());

CREATE POLICY "Sermons lecture publique"
ON sermons FOR SELECT USING (true);

CREATE POLICY "Sermons gestion admin, regisseur et editeur"
ON sermons FOR ALL USING (public.is_editor() OR public.is_producer());

CREATE POLICY "MediaAssets lecture publique"
ON media_assets FOR SELECT USING (status = 'ready');

CREATE POLICY "MediaAssets gestion equipe media"
ON media_assets FOR ALL USING (public.is_editor() OR public.is_producer());

CREATE POLICY "PlatformReplays lecture publique"
ON platform_replays FOR SELECT USING (true);

CREATE POLICY "PlatformReplays gestion equipe media"
ON platform_replays FOR ALL USING (public.is_editor() OR public.is_producer());

CREATE POLICY "BibleBooks lecture publique"
ON bible_books FOR SELECT USING (true);

CREATE POLICY "BibleVerses lecture publique"
ON bible_verses FOR SELECT USING (true);

-- 5. POLITIQUES RLS : DONS (DONATIONS)
CREATE POLICY "Tout le monde peut créer une intention de don"
ON donations FOR INSERT WITH CHECK (true);

CREATE POLICY "Le donateur peut consulter ses propres dons"
ON donations FOR SELECT TO authenticated
USING (auth.uid() = donor_id);

CREATE POLICY "Le tresorier et l'admin consultent tous les dons"
ON donations FOR SELECT TO authenticated
USING (public.is_treasurer());

-- Seul le service-role ou le backend via trigger/Edge Function peut mettre à jour le statut des dons

-- 6. POLITIQUES RLS : TRÉSORERIE & RETRAITS SÉCURISÉS (Section 20)
CREATE POLICY "Parametres tresorerie visibles uniquement par tresorier et admin"
ON treasury_settings FOR SELECT TO authenticated
USING (public.is_treasurer());

CREATE POLICY "Parametres tresorerie modifiables uniquement par admin"
ON treasury_settings FOR ALL TO authenticated
USING (public.is_admin());

CREATE POLICY "Retraits consultables uniquement par tresorier et admin"
ON withdrawal_requests FOR SELECT TO authenticated
USING (public.is_treasurer());

CREATE POLICY "Retraits initiables par le tresorier ou admin"
ON withdrawal_requests FOR INSERT TO authenticated
WITH CHECK (public.is_treasurer() AND auth.uid() = requested_by);

-- 7. POLITIQUES RLS : AUDIT LOGS
CREATE POLICY "Audit logs consultables uniquement par tresorier et admin"
ON audit_logs FOR SELECT TO authenticated
USING (public.is_treasurer());

CREATE POLICY "Insertion de logs autorisee pour les utilisateurs authentifies"
ON audit_logs FOR INSERT TO authenticated
WITH CHECK (true);
