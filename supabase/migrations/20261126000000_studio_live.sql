-- Annonce de direct lue par EECC TV. Une seule ligne, mise à jour par la régie.

create table if not exists public.studio_live (
    id integer primary key default 1 check (id = 1),
    is_live boolean not null default false,
    title text not null default '',
    speaker text not null default '',
    playback_url text,
    updated_at timestamptz not null default now()
);

insert into public.studio_live (id, is_live, title, speaker, playback_url)
values (1, false, '', '', null)
on conflict (id) do nothing;

alter table public.studio_live enable row level security;

drop policy if exists "Lecture du direct EECC TV" on public.studio_live;
create policy "Lecture du direct EECC TV"
on public.studio_live
for select
to anon, authenticated
using (true);

create or replace function public.publish_studio_live(
    p_title text,
    p_speaker text,
    p_url text,
    p_live boolean
)
returns public.studio_live
language plpgsql
security definer
set search_path = public
as $$
declare
    row public.studio_live;
begin
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

revoke all on function public.publish_studio_live(text, text, text, boolean) from public;
grant execute on function public.publish_studio_live(text, text, text, boolean) to anon, authenticated;
grant select on public.studio_live to anon, authenticated;
