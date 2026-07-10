-- =====================================================================
-- Campos e tabelas das novas features (lote de 30)
-- =====================================================================

-- itens
alter table public.itens add column if not exists codigo_barras text;
alter table public.itens add column if not exists marca text;
alter table public.itens add column if not exists fabricante text;
alter table public.itens add column if not exists observacoes_internas text;
alter table public.itens add column if not exists ponto_reposicao numeric(14,3) not null default 0;
alter table public.itens add column if not exists estoque_maximo numeric(14,3) not null default 0;
alter table public.itens add column if not exists custo_medio numeric(14,2) not null default 0;
alter table public.itens add column if not exists ativo boolean not null default true;

-- movimentacoes
alter table public.movimentacoes add column if not exists motivo text;
alter table public.movimentacoes add column if not exists custo_unitario numeric(14,2);

-- pedidos_compra
create sequence if not exists public.seq_pedido_numero;
alter table public.pedidos_compra add column if not exists numero bigint;
alter table public.pedidos_compra alter column numero set default nextval('public.seq_pedido_numero');
update public.pedidos_compra set numero = nextval('public.seq_pedido_numero') where numero is null;
alter table public.pedidos_compra add column if not exists preco_estimado numeric(14,2) not null default 0;
alter table public.pedidos_compra add column if not exists quantidade_recebida numeric(14,3) not null default 0;
alter table public.pedidos_compra add column if not exists anexo_url text;

-- requisicoes
alter table public.requisicoes add column if not exists centro_custo text;
alter table public.requisicao_itens add column if not exists quantidade_atendida numeric(14,3) not null default 0;

-- configuracoes (linha única)
create table if not exists public.configuracoes (
  id                   boolean primary key default true check (id),
  empresa_nome         text not null default 'Minha Empresa',
  moeda                text not null default 'BRL',
  dias_alerta_validade int not null default 30,
  atualizado_em        timestamptz not null default now()
);
insert into public.configuracoes (id) values (true) on conflict (id) do nothing;

-- catálogo fornecedor x item
create table if not exists public.fornecedor_itens (
  id                uuid primary key default gen_random_uuid(),
  fornecedor_id     uuid not null references public.fornecedores(id) on delete cascade,
  item_id           uuid not null references public.itens(id) on delete cascade,
  preco             numeric(14,2) not null default 0,
  codigo_fornecedor text,
  criado_em         timestamptz not null default now(),
  unique (fornecedor_id, item_id)
);

-- auditoria
create table if not exists public.auditoria (
  id          uuid primary key default gen_random_uuid(),
  usuario_id  uuid references public.profiles(id) on delete set null,
  acao        text not null,
  entidade    text not null,
  entidade_id uuid,
  descricao   text,
  criado_em   timestamptz not null default now()
);
create index if not exists idx_auditoria_data on public.auditoria(criado_em desc);

-- RLS
alter table public.configuracoes enable row level security;
alter table public.fornecedor_itens enable row level security;
alter table public.auditoria enable row level security;

drop policy if exists config_select on public.configuracoes;
create policy config_select on public.configuracoes for select using (auth.uid() is not null);
drop policy if exists config_update on public.configuracoes;
create policy config_update on public.configuracoes for update
  using (public.tem_papel(array['gerente']::papel_usuario[]));

drop policy if exists fi_select on public.fornecedor_itens;
create policy fi_select on public.fornecedor_itens for select using (auth.uid() is not null);
drop policy if exists fi_write on public.fornecedor_itens;
create policy fi_write on public.fornecedor_itens for all
  using (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]))
  with check (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]));

drop policy if exists aud_select on public.auditoria;
create policy aud_select on public.auditoria for select
  using (public.tem_papel(array['gerente']::papel_usuario[]));
