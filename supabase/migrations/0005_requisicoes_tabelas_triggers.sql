-- =====================================================================
-- Requisições de material
-- =====================================================================
-- Funcionário solicita itens do estoque; almoxarife/gerente aprova e
-- atende. Ao atender, gera saída automática de estoque para cada item.

do $$ begin
  create type status_requisicao as enum ('pendente','aprovada','rejeitada','atendida','cancelada');
exception when duplicate_object then null; end $$;

create table if not exists public.requisicoes (
  id                uuid primary key default gen_random_uuid(),
  solicitante_id    uuid references public.profiles(id) on delete set null,
  setor             text,
  justificativa     text,
  status            status_requisicao not null default 'pendente',
  aprovador_id      uuid references public.profiles(id) on delete set null,
  observacao_gestor text,
  criado_em         timestamptz not null default now(),
  atualizado_em     timestamptz not null default now()
);
create index if not exists idx_requisicoes_status on public.requisicoes(status);

create table if not exists public.requisicao_itens (
  id             uuid primary key default gen_random_uuid(),
  requisicao_id  uuid not null references public.requisicoes(id) on delete cascade,
  item_id        uuid not null references public.itens(id) on delete restrict,
  quantidade     numeric(14,3) not null check (quantidade > 0)
);
create index if not exists idx_req_itens_req on public.requisicao_itens(requisicao_id);

create or replace function public.notificar_nova_requisicao()
returns trigger
language plpgsql security definer set search_path = public
as $$
declare v_solic text;
begin
  select nome into v_solic from public.profiles where id = new.solicitante_id;
  perform public.notificar_papeis(
    array['gerente','almoxarife']::papel_usuario[],
    'nova_requisicao', 'Nova requisição de material',
    coalesce(v_solic,'Alguem') || ' abriu uma requisição de material' ||
      case when new.setor is not null then ' para o setor ' || new.setor else '' end || '.',
    null, null
  );
  return new;
end;
$$;

drop trigger if exists trg_nova_requisicao on public.requisicoes;
create trigger trg_nova_requisicao
  after insert on public.requisicoes
  for each row execute function public.notificar_nova_requisicao();

create or replace function public.processar_status_requisicao()
returns trigger
language plpgsql security definer set search_path = public
as $$
declare
  r record;
  v_tipo tipo_notificacao;
  v_titulo text;
begin
  if new.status = old.status then
    return new;
  end if;
  new.atualizado_em := now();

  -- Ao atender: gera saída de estoque para cada item (rollback se faltar estoque)
  if new.status = 'atendida' then
    for r in select * from public.requisicao_itens where requisicao_id = new.id loop
      insert into public.movimentacoes (item_id, tipo, quantidade, observacao, usuario_id)
      values (r.item_id, 'saida', r.quantidade, 'Atendimento de requisição', auth.uid());
    end loop;
  end if;

  if new.status = 'aprovada' then
    v_tipo := 'requisicao_aprovada'; v_titulo := 'Requisição aprovada';
  elsif new.status = 'rejeitada' then
    v_tipo := 'requisicao_rejeitada'; v_titulo := 'Requisição rejeitada';
  elsif new.status = 'atendida' then
    v_tipo := 'requisicao_atendida'; v_titulo := 'Requisição atendida';
  else
    return new;
  end if;

  if new.solicitante_id is not null then
    insert into public.notificacoes (usuario_id, tipo, titulo, mensagem)
    values (new.solicitante_id, v_tipo, v_titulo,
            'Sua requisição de material foi ' || new.status::text || '.');
  end if;

  return new;
end;
$$;

drop trigger if exists trg_status_requisicao on public.requisicoes;
create trigger trg_status_requisicao
  before update on public.requisicoes
  for each row execute function public.processar_status_requisicao();

revoke execute on function public.notificar_nova_requisicao() from public, anon, authenticated;
revoke execute on function public.processar_status_requisicao() from public, anon, authenticated;

-- RLS
alter table public.requisicoes enable row level security;
alter table public.requisicao_itens enable row level security;

drop policy if exists req_select on public.requisicoes;
create policy req_select on public.requisicoes
  for select using (auth.uid() is not null);

drop policy if exists req_insert on public.requisicoes;
create policy req_insert on public.requisicoes
  for insert with check (solicitante_id = auth.uid());

drop policy if exists req_update_gestor on public.requisicoes;
create policy req_update_gestor on public.requisicoes
  for update using (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]));

drop policy if exists req_update_dono on public.requisicoes;
create policy req_update_dono on public.requisicoes
  for update using (solicitante_id = auth.uid() and status = 'pendente');

drop policy if exists reqitens_select on public.requisicao_itens;
create policy reqitens_select on public.requisicao_itens
  for select using (auth.uid() is not null);

drop policy if exists reqitens_insert on public.requisicao_itens;
create policy reqitens_insert on public.requisicao_itens
  for insert with check (exists (
    select 1 from public.requisicoes r
    where r.id = requisicao_id and r.solicitante_id = auth.uid()
  ));
