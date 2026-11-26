-- Réserve la publication à la régie et ouvre le flux public hors Wi-Fi.

create table if not exists public.studio_gate (
    id integer primary key check (id = 1),
    secret_hash text
);

alter table public.studio_gate enable row level security;

insert into public.studio_gate (id, secret_hash)
values (1, 'bac1cdd6273ef3b202ff25d7b308a2b4ee8700ead0b2fabd05ed565ff9f2594b')
on conflict (id) do update set secret_hash = excluded.secret_hash
where public.studio_gate.secret_hash is null;

create or replace function public.studio_secret_ok(p_secret text)
returns boolean
language plpgsql
security definer
set search_path = public, extensions
as $$
declare
    stored text;
    given text;
begin
    if p_secret is null or length(p_secret) < 16 then
        return false;
    end if;
    given := encode(digest(p_secret, 'sha256'), 'hex');
    select secret_hash into stored from public.studio_gate where id = 1;
    return stored is not null and stored = given;
end;
$$;

revoke all on function public.studio_secret_ok(text) from public;

drop function if exists public.publish_studio_live(text, text, text, boolean);

create or replace function public.publish_studio_live(
    p_title text,
    p_speaker text,
    p_url text,
    p_live boolean,
    p_secret text
)
returns public.studio_live
language plpgsql
security definer
set search_path = public
as $$
declare
    row public.studio_live;
begin
    if not public.studio_secret_ok(p_secret) then
        raise exception 'Secret de régie refusé';
    end if;

    insert into public.studio_live (id, is_live, title, speaker, playback_url, updated_at)
    values (
        1,
        coalesce(p_live, false),
        left(coalesce(p_title, ''), 180),
        left(coalesce(p_speaker, ''), 120),
        nullif(left(coalesce(p_url, ''), 500), ''),
        now()
    )
    on conflict (id) do update set
        is_live = excluded.is_live,
        title = excluded.title,
        speaker = excluded.speaker,
        playback_url = excluded.playback_url,
        updated_at = now()
    returning * into row;

    if row.is_live and row.playback_url is not null then
        update public.events
        set status = 'ended', updated_at = now()
        where status = 'live';

        insert into public.events (title, description, start_time, status, live_stream_url, event_type)
        values (
            coalesce(nullif(row.title, ''), 'Culte en direct'),
            row.speaker,
            now(),
            'live',
            row.playback_url,
            'worship_service'
        );
    elsif not row.is_live then
        update public.events
        set status = 'ended', updated_at = now()
        where status = 'live';
    end if;

    return row;
end;
$$;

create or replace function public.studio_save_content(
    p_secret text,
    p_kind text,
    p_row jsonb
)
returns uuid
language plpgsql
security definer
set search_path = public
as $$
declare
    item_id uuid := coalesce(nullif(p_row->>'id', '')::uuid, gen_random_uuid());
begin
    if not public.studio_secret_ok(p_secret) then
        raise exception 'Secret de régie refusé';
    end if;

    if p_kind = 'sermons' then
        insert into public.sermons (id, title, speaker, date, theme, scripture_refs, summary, full_transcript, podcast_url, cover_image_url, is_published, reading_time_minutes, podcast_duration_seconds)
        values (
            item_id,
            coalesce(nullif(p_row->>'title', ''), 'Sans titre'),
            coalesce(nullif(p_row->>'speaker', ''), 'EECC'),
            coalesce((nullif(p_row->>'date', ''))::date, current_date),
            p_row->>'theme',
            p_row->>'scripture_refs',
            p_row->>'summary',
            p_row->>'full_transcript',
            coalesce(p_row->>'podcast_url', p_row->>'audio_url'),
            p_row->>'cover_image_url',
            coalesce((p_row->>'is_published')::boolean, true),
            coalesce((p_row->>'reading_time_minutes')::int, 5),
            coalesce((p_row->>'podcast_duration_seconds')::int, (p_row->>'duration_seconds')::int, 0)
        )
        on conflict (id) do update set
            title = coalesce(nullif(excluded.title, 'Sans titre'), sermons.title),
            speaker = coalesce(nullif(excluded.speaker, ''), sermons.speaker),
            theme = coalesce(excluded.theme, sermons.theme),
            scripture_refs = coalesce(excluded.scripture_refs, sermons.scripture_refs),
            summary = coalesce(excluded.summary, sermons.summary),
            full_transcript = coalesce(excluded.full_transcript, sermons.full_transcript),
            podcast_url = coalesce(excluded.podcast_url, sermons.podcast_url),
            cover_image_url = coalesce(excluded.cover_image_url, sermons.cover_image_url),
            is_published = coalesce((p_row->>'is_published')::boolean, sermons.is_published),
            updated_at = now();
    elsif p_kind = 'audio_summaries' then
        insert into public.audio_summaries (id, title, speaker, date, description, audio_url, duration_seconds, cover_image_url, is_published)
        values (
            item_id,
            coalesce(nullif(p_row->>'title', ''), 'Capsule'),
            coalesce(nullif(p_row->>'speaker', ''), 'EECC'),
            coalesce((nullif(p_row->>'date', ''))::date, current_date),
            p_row->>'description',
            coalesce(nullif(p_row->>'audio_url', ''), 'https://rwojdiomftillhsamjal.supabase.co'),
            coalesce((p_row->>'duration_seconds')::int, 0),
            p_row->>'cover_image_url',
            coalesce((p_row->>'is_published')::boolean, true)
        )
        on conflict (id) do update set
            title = coalesce(nullif(p_row->>'title', ''), audio_summaries.title),
            speaker = coalesce(nullif(p_row->>'speaker', ''), audio_summaries.speaker),
            description = coalesce(p_row->>'description', audio_summaries.description),
            audio_url = coalesce(nullif(p_row->>'audio_url', ''), audio_summaries.audio_url),
            is_published = coalesce((p_row->>'is_published')::boolean, audio_summaries.is_published),
            updated_at = now();
    elsif p_kind = 'short_videos' then
        insert into public.short_videos (id, title, description, speaker, date, video_url, thumbnail_url, duration_seconds, aspect_ratio, is_published)
        values (
            item_id,
            coalesce(nullif(p_row->>'title', ''), 'Clip'),
            p_row->>'description',
            p_row->>'speaker',
            coalesce((nullif(p_row->>'date', ''))::date, current_date),
            coalesce(nullif(p_row->>'video_url', ''), 'https://rwojdiomftillhsamjal.supabase.co'),
            p_row->>'thumbnail_url',
            coalesce((p_row->>'duration_seconds')::int, 0),
            coalesce(nullif(p_row->>'aspect_ratio', ''), '9:16'),
            coalesce((p_row->>'is_published')::boolean, true)
        )
        on conflict (id) do update set
            title = coalesce(nullif(p_row->>'title', ''), short_videos.title),
            description = coalesce(p_row->>'description', short_videos.description),
            video_url = coalesce(nullif(p_row->>'video_url', ''), short_videos.video_url),
            is_published = coalesce((p_row->>'is_published')::boolean, short_videos.is_published),
            updated_at = now();
    elsif p_kind = 'event_banners' then
        insert into public.event_banners (id, title, subtitle, image_url, link_url, display_order, is_published)
        values (
            item_id,
            coalesce(nullif(p_row->>'title', ''), 'Affiche'),
            p_row->>'subtitle',
            coalesce(nullif(p_row->>'image_url', ''), 'https://rwojdiomftillhsamjal.supabase.co'),
            p_row->>'link_url',
            coalesce((p_row->>'display_order')::int, 0),
            coalesce((p_row->>'is_published')::boolean, true)
        )
        on conflict (id) do update set
            title = coalesce(nullif(p_row->>'title', ''), event_banners.title),
            subtitle = coalesce(p_row->>'subtitle', event_banners.subtitle),
            image_url = coalesce(nullif(p_row->>'image_url', ''), event_banners.image_url),
            is_published = coalesce((p_row->>'is_published')::boolean, event_banners.is_published),
            updated_at = now();
    elsif p_kind = 'photo_albums' then
        insert into public.photo_albums (id, title, description, date, cover_image_url, photo_count, is_published)
        values (
            item_id,
            coalesce(nullif(p_row->>'title', ''), 'Album'),
            p_row->>'description',
            coalesce((nullif(p_row->>'date', ''))::date, current_date),
            p_row->>'cover_image_url',
            coalesce((p_row->>'photo_count')::int, 0),
            coalesce((p_row->>'is_published')::boolean, true)
        )
        on conflict (id) do update set
            title = coalesce(nullif(p_row->>'title', ''), photo_albums.title),
            description = coalesce(p_row->>'description', photo_albums.description),
            photo_count = coalesce((p_row->>'photo_count')::int, photo_albums.photo_count),
            is_published = coalesce((p_row->>'is_published')::boolean, photo_albums.is_published),
            updated_at = now();
    elsif p_kind = 'album_photos' then
        insert into public.album_photos (id, album_id, photo_url, thumbnail_url, caption, display_order, is_published)
        values (
            item_id,
            (p_row->>'album_id')::uuid,
            coalesce(nullif(p_row->>'photo_url', ''), 'https://rwojdiomftillhsamjal.supabase.co'),
            p_row->>'thumbnail_url',
            p_row->>'caption',
            coalesce((p_row->>'display_order')::int, 0),
            coalesce((p_row->>'is_published')::boolean, true)
        )
        on conflict (id) do update set
            caption = coalesce(p_row->>'caption', album_photos.caption),
            photo_url = coalesce(nullif(p_row->>'photo_url', ''), album_photos.photo_url),
            is_published = coalesce((p_row->>'is_published')::boolean, album_photos.is_published);
    else
        raise exception 'Type de contenu inconnu';
    end if;

    return item_id;
end;
$$;

create or replace function public.studio_remove_content(
    p_secret text,
    p_kind text,
    p_id uuid
)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
    if not public.studio_secret_ok(p_secret) then
        raise exception 'Secret de régie refusé';
    end if;

    if p_kind = 'sermons' then delete from public.sermons where id = p_id;
    elsif p_kind = 'audio_summaries' then delete from public.audio_summaries where id = p_id;
    elsif p_kind = 'short_videos' then delete from public.short_videos where id = p_id;
    elsif p_kind = 'event_banners' then delete from public.event_banners where id = p_id;
    elsif p_kind = 'photo_albums' then delete from public.photo_albums where id = p_id;
    elsif p_kind = 'album_photos' then delete from public.album_photos where id = p_id;
    else raise exception 'Type de contenu inconnu';
    end if;
end;
$$;

revoke all on function public.publish_studio_live(text, text, text, boolean, text) from public;
revoke all on function public.studio_save_content(text, text, jsonb) from public;
revoke all on function public.studio_remove_content(text, text, uuid) from public;
grant execute on function public.publish_studio_live(text, text, text, boolean, text) to anon, authenticated;
grant execute on function public.studio_save_content(text, text, jsonb) to anon, authenticated;
grant execute on function public.studio_remove_content(text, text, uuid) to anon, authenticated;

insert into storage.buckets (id, name, public)
values ('live', 'live', true)
on conflict (id) do update set public = true;

drop policy if exists "Lecture du direct public" on storage.objects;
create policy "Lecture du direct public"
on storage.objects for select
to public
using (bucket_id = 'live');

drop policy if exists "Ecriture du direct public" on storage.objects;
create policy "Ecriture du direct public"
on storage.objects for insert
to anon, authenticated
with check (bucket_id in ('live', 'podcasts', 'audio-summaries', 'short-videos', 'banners', 'photo-galleries'));

drop policy if exists "Mise a jour du direct public" on storage.objects;
create policy "Mise a jour du direct public"
on storage.objects for update
to anon, authenticated
using (bucket_id in ('live', 'podcasts', 'audio-summaries', 'short-videos', 'banners', 'photo-galleries'));

drop policy if exists "Suppression media studio" on storage.objects;
create policy "Suppression media studio"
on storage.objects for delete
to anon, authenticated
using (bucket_id in ('live', 'podcasts', 'audio-summaries', 'short-videos', 'banners', 'photo-galleries'));
