-- ==============================================================================
-- EECC Ecosystem - Migration 000002 : Triggers, Fonctions d'Audit et Sécurité OTP
-- ==============================================================================

-- 1. FONCTION ET TRIGGER POUR MISE À JOUR AUTOMATIQUE DE updated_at
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER update_profiles_updated_at BEFORE UPDATE ON profiles
FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_churches_updated_at BEFORE UPDATE ON churches
FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_events_updated_at BEFORE UPDATE ON events
FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_sermons_updated_at BEFORE UPDATE ON sermons
FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_media_assets_updated_at BEFORE UPDATE ON media_assets
FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_platform_replays_updated_at BEFORE UPDATE ON platform_replays
FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_donations_updated_at BEFORE UPDATE ON donations
FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_treasury_settings_updated_at BEFORE UPDATE ON treasury_settings
FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_withdrawal_requests_updated_at BEFORE UPDATE ON withdrawal_requests
FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- 2. CRÉATION AUTOMATIQUE DU PROFIL À L'INSCRIPTION
CREATE OR REPLACE FUNCTION public.handle_new_user()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO public.profiles (id, display_name, email)
    VALUES (
        NEW.id,
        COALESCE(NEW.raw_user_meta_data->>'display_name', split_part(NEW.email, '@', 1)),
        NEW.email
    );

    -- Attribuer le rôle par défaut 'fidele'
    INSERT INTO public.user_roles (user_id, role)
    VALUES (NEW.id, 'fidele');

    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE OR REPLACE TRIGGER on_auth_user_created
AFTER INSERT ON auth.users
FOR EACH ROW EXECUTE FUNCTION public.handle_new_user();

-- 3. FONCTION DE JOURNALISATION D'AUDIT SÉCURISÉE
CREATE OR REPLACE FUNCTION public.log_audit_event(
    p_actor_id UUID,
    p_action TEXT,
    p_entity_type TEXT,
    p_entity_id TEXT,
    p_metadata JSONB DEFAULT '{}'::jsonb
)
RETURNS VOID AS $$
BEGIN
    INSERT INTO public.audit_logs (
        actor_id,
        action,
        entity_type,
        entity_id,
        metadata
    ) VALUES (
        p_actor_id,
        p_action,
        p_entity_type,
        p_entity_id,
        p_metadata
    );
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

-- 4. TRIGGER D'AUDIT AUTOMATIQUE SUR LES CHANGEMENTS D'ÉTAT DES RETRAITS
CREATE OR REPLACE FUNCTION public.audit_withdrawal_status_change()
RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'UPDATE' AND OLD.status IS DISTINCT FROM NEW.status) THEN
        PERFORM public.log_audit_event(
            COALESCE(auth.uid(), NEW.requested_by),
            'WITHDRAWAL_STATUS_CHANGED',
            'withdrawal_requests',
            NEW.id::TEXT,
            jsonb_build_object(
                'reference', NEW.reference,
                'old_status', OLD.status,
                'new_status', NEW.status,
                'amount_gross', NEW.amount_gross,
                'currency', NEW.currency,
                'destination_provider', NEW.destination_provider
            )
        );
    ELSIF (TG_OP = 'INSERT') THEN
        PERFORM public.log_audit_event(
            NEW.requested_by,
            'WITHDRAWAL_REQUEST_CREATED',
            'withdrawal_requests',
            NEW.id::TEXT,
            jsonb_build_object(
                'reference', NEW.reference,
                'status', NEW.status,
                'amount_gross', NEW.amount_gross,
                'currency', NEW.currency,
                'destination_provider', NEW.destination_provider
            )
        );
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql SECURITY DEFINER;

CREATE TRIGGER on_withdrawal_request_audit
AFTER INSERT OR UPDATE ON withdrawal_requests
FOR EACH ROW EXECUTE FUNCTION public.audit_withdrawal_status_change();
