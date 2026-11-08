-- =====================================================================
-- Almoxarifado & Compras — Schema inicial
-- =====================================================================
-- Papéis:
--   gerente     -> visão geral, aprova compras, gerencia usuários
--   almoxarife  -> responsável pelo almoxarifado (cadastro, movimentação)
--   funcionario -> consulta itens e abre pedidos de compra
-- =====================================================================

-- ---------- Tipos ----------------------------------------------------
do $$ begin
  create type papel_usuario as enum ('gerente', 'almoxarife', 'funcionario');
exception when duplicate_object then null; end $$;

do $$ begin
  create type tipo_movimentacao as enum ('entrada', 'saida', 'ajuste');
exception when duplicate_object then null; end $$;

do $$ begin
  create type status_pedido as enum ('pendente', 'aprovado', 'rejeitado', 'comprado', 'recebido', 'cancelado');
exception when duplicate_object then null; end $$;

do $$ begin
  create type tipo_notificacao as enum (
    'estoque_baixo', 'validade_proxima', 'validade_vencida',
    'novo_pedido', 'pedido_aprovado', 'pedido_rejeitado', 'pedido_recebido'
  );
exception when duplicate_object then null; end $$;

-- ---------- profiles -------------------------------------------------
create table if not exists public.profiles (
  id         uuid primary key references auth.users(id) on delete cascade,
  nome       text not null default '',
  email      text,
  papel      papel_usuario not null default 'funcionario',
  ativo      boolean not null default true,
  criado_em  timestamptz not null default now()
);

-- ---------- categorias ----------------------------------------------
create table if not exists public.categorias (
  id         uuid primary key default gen_random_uuid(),
  nome       text not null unique,
  descricao  text,
  criado_em  timestamptz not null default now()
);

-- ---------- itens ----------------------------------------------------
create table if not exists public.itens (
  id             uuid primary key default gen_random_uuid(),
  codigo         text not null unique,
  nome           text not null,
  descricao      text,
  categoria_id   uuid references public.categorias(id) on delete set null,
  unidade        text not null default 'un',
  localizacao    text,
  quantidade     numeric(14,3) not null default 0,
  estoque_minimo numeric(14,3) not null default 0,
  data_validade  date,
  criado_por     uuid references public.profiles(id) on delete set null,
  criado_em      timestamptz not null default now(),
  atualizado_em  timestamptz not null default now()
);
create index if not exists idx_itens_nome on public.itens using gin (to_tsvector('portuguese', nome || ' ' || coalesce(descricao,'')));
create index if not exists idx_itens_categoria on public.itens(categoria_id);

-- ---------- movimentacoes -------------------------------------------
create table if not exists public.movimentacoes (
  id          uuid primary key default gen_random_uuid(),
  item_id     uuid not null references public.itens(id) on delete cascade,
  tipo        tipo_movimentacao not null,
  quantidade  numeric(14,3) not null check (quantidade >= 0),
  observacao  text,
  usuario_id  uuid references public.profiles(id) on delete set null,
  criado_em   timestamptz not null default now()
);
create index if not exists idx_mov_item on public.movimentacoes(item_id);

-- ---------- pedidos_compra ------------------------------------------
create table if not exists public.pedidos_compra (
  id                    uuid primary key default gen_random_uuid(),
  item_id               uuid references public.itens(id) on delete set null,
  descricao_item        text not null,
  quantidade_solicitada numeric(14,3) not null check (quantidade_solicitada > 0),
  unidade               text default 'un',
  justificativa         text,
  status                status_pedido not null default 'pendente',
  solicitante_id        uuid references public.profiles(id) on delete set null,
  aprovador_id          uuid references public.profiles(id) on delete set null,
  observacao_gestor     text,
  criado_em             timestamptz not null default now(),
  atualizado_em         timestamptz not null default now()
);
create index if not exists idx_pedidos_status on public.pedidos_compra(status);

-- ---------- notificacoes --------------------------------------------
create table if not exists public.notificacoes (
  id          uuid primary key default gen_random_uuid(),
  usuario_id  uuid not null references public.profiles(id) on delete cascade,
  tipo        tipo_notificacao not null,
  titulo      text not null,
  mensagem    text not null,
  lida        boolean not null default false,
  item_id     uuid references public.itens(id) on delete cascade,
  pedido_id   uuid references public.pedidos_compra(id) on delete cascade,
  criado_em   timestamptz not null default now()
);
create index if not exists idx_notif_usuario on public.notificacoes(usuario_id, lida);

-- =====================================================================
-- Funções auxiliares
-- =====================================================================

-- Papel do usuário logado (SECURITY DEFINER evita recursão de RLS)
create or replace function public.meu_papel()
returns papel_usuario
language sql stable security definer set search_path = public
as $$
  select papel from public.profiles where id = auth.uid();
$$;

create or replace function public.tem_papel(papeis papel_usuario[])
returns boolean
language sql stable security definer set search_path = public
as $$
  select exists (
    select 1 from public.profiles
    where id = auth.uid() and papel = any(papeis)
  );
$$;

-- Cria uma notificação para todos os usuários ativos de um conjunto de papéis
create or replace function public.notificar_papeis(
  p_papeis papel_usuario[],
  p_tipo   tipo_notificacao,
  p_titulo text,
  p_msg    text,
  p_item   uuid default null,
  p_pedido uuid default null
) returns void
language plpgsql security definer set search_path = public
as $$
begin
  insert into public.notificacoes (usuario_id, tipo, titulo, mensagem, item_id, pedido_id)
  select p.id, p_tipo, p_titulo, p_msg, p_item, p_pedido
  from public.profiles p
  where p.ativo and p.papel = any(p_papeis);
end;
$$;

-- =====================================================================
-- Trigger: novo usuário -> profile
-- O primeiro usuário criado vira 'gerente'; os demais 'funcionario'.
-- =====================================================================
create or replace function public.handle_new_user()
returns trigger
language plpgsql security definer set search_path = public
as $$
declare
  v_papel papel_usuario;
begin
  if not exists (select 1 from public.profiles) then
    v_papel := 'gerente';
  else
    v_papel := 'funcionario';
  end if;

  insert into public.profiles (id, nome, email, papel)
  values (
    new.id,
    coalesce(new.raw_user_meta_data->>'nome', split_part(new.email, '@', 1)),
    new.email,
    v_papel
  )
  on conflict (id) do nothing;
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
  after insert on auth.users
  for each row execute function public.handle_new_user();

-- =====================================================================
-- Trigger: aplicar movimentação no estoque
-- =====================================================================
create or replace function public.aplicar_movimentacao()
returns trigger
language plpgsql security definer set search_path = public
as $$
declare
  v_atual numeric(14,3);
begin
  select quantidade into v_atual from public.itens where id = new.item_id for update;

  if new.tipo = 'entrada' then
    v_atual := v_atual + new.quantidade;
  elsif new.tipo = 'saida' then
    if v_atual - new.quantidade < 0 then
      raise exception 'Estoque insuficiente: disponível %, solicitado %', v_atual, new.quantidade;
    end if;
    v_atual := v_atual - new.quantidade;
  elsif new.tipo = 'ajuste' then
    v_atual := new.quantidade; -- ajuste define o valor absoluto
  end if;

  update public.itens
    set quantidade = v_atual, atualizado_em = now()
    where id = new.item_id;

  return new;
end;
$$;

drop trigger if exists trg_aplicar_movimentacao on public.movimentacoes;
create trigger trg_aplicar_movimentacao
  after insert on public.movimentacoes
  for each row execute function public.aplicar_movimentacao();

-- =====================================================================
-- Trigger: alerta de estoque baixo (ao criar/atualizar item)
-- =====================================================================
create or replace function public.verificar_estoque_baixo()
returns trigger
language plpgsql security definer set search_path = public
as $$
declare
  cruzou boolean;
begin
  if tg_op = 'INSERT' then
    cruzou := new.quantidade <= new.estoque_minimo;
  else
    cruzou := new.quantidade <= new.estoque_minimo
              and (old.quantidade > old.estoque_minimo or old.quantidade > new.estoque_minimo);
  end if;

  if cruzou and new.estoque_minimo > 0 then
    perform public.notificar_papeis(
      array['gerente','almoxarife']::papel_usuario[],
      'estoque_baixo',
      'Estoque baixo: ' || new.nome,
      'O item "' || new.nome || '" (cód. ' || new.codigo || ') está com ' ||
        trim(to_char(new.quantidade, 'FM999999990.###')) || ' ' || new.unidade ||
        ', igual ou abaixo do mínimo de ' || trim(to_char(new.estoque_minimo, 'FM999999990.###')) || '.',
      new.id, null
    );
  end if;
  return new;
end;
$$;

drop trigger if exists trg_estoque_baixo on public.itens;
create trigger trg_estoque_baixo
  after insert or update of quantidade, estoque_minimo on public.itens
  for each row execute function public.verificar_estoque_baixo();

-- =====================================================================
-- Trigger: notificar novo pedido de compra
-- =====================================================================
create or replace function public.notificar_novo_pedido()
returns trigger
language plpgsql security definer set search_path = public
as $$
declare
  v_solic text;
begin
  select nome into v_solic from public.profiles where id = new.solicitante_id;
  perform public.notificar_papeis(
    array['gerente','almoxarife']::papel_usuario[],
    'novo_pedido',
    'Novo pedido de compra',
    coalesce(v_solic,'Alguém') || ' solicitou ' ||
      trim(to_char(new.quantidade_solicitada, 'FM999999990.###')) || ' ' || coalesce(new.unidade,'un') ||
      ' de "' || new.descricao_item || '".',
    new.item_id, new.id
  );
  return new;
end;
$$;

drop trigger if exists trg_novo_pedido on public.pedidos_compra;
create trigger trg_novo_pedido
  after insert on public.pedidos_compra
  for each row execute function public.notificar_novo_pedido();

-- =====================================================================
-- Trigger: notificar mudança de status do pedido
-- =====================================================================
create or replace function public.notificar_status_pedido()
returns trigger
language plpgsql security definer set search_path = public
as $$
declare
  v_tipo tipo_notificacao;
  v_titulo text;
begin
  if new.status = old.status then
    return new;
  end if;

  new.atualizado_em := now();

  if new.status = 'aprovado' then
    v_tipo := 'pedido_aprovado'; v_titulo := 'Pedido aprovado';
  elsif new.status = 'rejeitado' then
    v_tipo := 'pedido_rejeitado'; v_titulo := 'Pedido rejeitado';
  elsif new.status = 'recebido' then
    v_tipo := 'pedido_recebido'; v_titulo := 'Pedido recebido';
  else
    return new; -- comprado/cancelado sem notificação dedicada
  end if;

  if new.solicitante_id is not null then
    insert into public.notificacoes (usuario_id, tipo, titulo, mensagem, item_id, pedido_id)
    values (
      new.solicitante_id, v_tipo, v_titulo,
      'Seu pedido de "' || new.descricao_item || '" foi ' ||
        replace(new.status::text, 'rejeitado', 'rejeitado') || '.',
      new.item_id, new.id
    );
  end if;

  -- pedido recebido também avisa gerente/almoxarife
  if new.status = 'recebido' then
    perform public.notificar_papeis(
      array['gerente','almoxarife']::papel_usuario[],
      'pedido_recebido', 'Pedido recebido',
      'O pedido de "' || new.descricao_item || '" foi recebido no almoxarifado.',
      new.item_id, new.id
    );
  end if;

  return new;
end;
$$;

drop trigger if exists trg_status_pedido on public.pedidos_compra;
create trigger trg_status_pedido
  before update on public.pedidos_compra
  for each row execute function public.notificar_status_pedido();

-- =====================================================================
-- Função: gerar alertas de validade (idempotente — segura p/ chamar no load)
-- Cria no máx. 1 notificação por item/tipo a cada 24h.
-- =====================================================================
create or replace function public.gerar_alertas_validade(p_dias int default 30)
returns void
language plpgsql security definer set search_path = public
as $$
declare
  r record;
  v_tipo tipo_notificacao;
  v_titulo text;
  v_msg text;
begin
  for r in
    select * from public.itens
    where data_validade is not null
      and data_validade <= current_date + p_dias
  loop
    if r.data_validade < current_date then
      v_tipo := 'validade_vencida';
      v_titulo := 'Validade VENCIDA: ' || r.nome;
      v_msg := 'O item "' || r.nome || '" (cód. ' || r.codigo || ') venceu em ' ||
               to_char(r.data_validade, 'DD/MM/YYYY') || '.';
    else
      v_tipo := 'validade_proxima';
      v_titulo := 'Validade próxima: ' || r.nome;
      v_msg := 'O item "' || r.nome || '" (cód. ' || r.codigo || ') vence em ' ||
               to_char(r.data_validade, 'DD/MM/YYYY') || '.';
    end if;

    -- evita duplicar: já existe notificação do mesmo tipo p/ este item nas últimas 24h?
    if not exists (
      select 1 from public.notificacoes n
      where n.item_id = r.id and n.tipo = v_tipo
        and n.criado_em > now() - interval '24 hours'
    ) then
      perform public.notificar_papeis(
        array['gerente','almoxarife']::papel_usuario[],
        v_tipo, v_titulo, v_msg, r.id, null
      );
    end if;
  end loop;
end;
$$;

-- =====================================================================
-- RLS
-- =====================================================================
alter table public.profiles       enable row level security;
alter table public.categorias     enable row level security;
alter table public.itens          enable row level security;
alter table public.movimentacoes  enable row level security;
alter table public.pedidos_compra enable row level security;
alter table public.notificacoes   enable row level security;

-- profiles
drop policy if exists profiles_select on public.profiles;
create policy profiles_select on public.profiles
  for select using (auth.uid() is not null);

drop policy if exists profiles_update_self on public.profiles;
create policy profiles_update_self on public.profiles
  for update using (id = auth.uid());

drop policy if exists profiles_update_gerente on public.profiles;
create policy profiles_update_gerente on public.profiles
  for update using (public.tem_papel(array['gerente']::papel_usuario[]));

-- categorias
drop policy if exists categorias_select on public.categorias;
create policy categorias_select on public.categorias
  for select using (auth.uid() is not null);

drop policy if exists categorias_write on public.categorias;
create policy categorias_write on public.categorias
  for all using (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]))
  with check (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]));

-- itens (todos leem; almoxarife/gerente escrevem)
drop policy if exists itens_select on public.itens;
create policy itens_select on public.itens
  for select using (auth.uid() is not null);

drop policy if exists itens_write on public.itens;
create policy itens_write on public.itens
  for all using (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]))
  with check (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]));

-- movimentacoes (todos leem; almoxarife/gerente inserem)
drop policy if exists mov_select on public.movimentacoes;
create policy mov_select on public.movimentacoes
  for select using (auth.uid() is not null);

drop policy if exists mov_insert on public.movimentacoes;
create policy mov_insert on public.movimentacoes
  for insert with check (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]));

-- pedidos_compra
drop policy if exists pedidos_select on public.pedidos_compra;
create policy pedidos_select on public.pedidos_compra
  for select using (auth.uid() is not null);

drop policy if exists pedidos_insert on public.pedidos_compra;
create policy pedidos_insert on public.pedidos_compra
  for insert with check (solicitante_id = auth.uid());

drop policy if exists pedidos_update_gestor on public.pedidos_compra;
create policy pedidos_update_gestor on public.pedidos_compra
  for update using (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]));

drop policy if exists pedidos_update_dono on public.pedidos_compra;
create policy pedidos_update_dono on public.pedidos_compra
  for update using (solicitante_id = auth.uid() and status = 'pendente');

-- notificacoes (cada um vê/atualiza as suas)
drop policy if exists notif_select on public.notificacoes;
create policy notif_select on public.notificacoes
  for select using (usuario_id = auth.uid());

drop policy if exists notif_update on public.notificacoes;
create policy notif_update on public.notificacoes
  for update using (usuario_id = auth.uid());
