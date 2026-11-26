alter table public.studio_live add column if not exists theme text not null default '';

create or replace function public.studio_set_live_caption(
    p_title text,
    p_speaker text,
    p_theme text,
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

    update public.studio_live
    set title = left(coalesce(p_title, ''), 180),
        speaker = left(coalesce(p_speaker, ''), 120),
        theme = left(coalesce(p_theme, ''), 180),
        updated_at = now()
    where id = 1
    returning * into row;

    update public.events
    set title = coalesce(nullif(row.title, ''), title),
        description = coalesce(nullif(row.speaker, ''), description),
        updated_at = now()
    where status = 'live';

    return row;
end;
$$;

revoke all on function public.studio_set_live_caption(text, text, text, text) from public;
grant execute on function public.studio_set_live_caption(text, text, text, text) to anon, authenticated;

do $$
begin
    alter publication supabase_realtime add table public.studio_live;
exception
    when duplicate_object then null;
    when undefined_object then null;
end $$;
