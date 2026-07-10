-- =====================================================================
-- Lotes / validade por lote + fornecedores B2B (CNPJ) e preço por lote
-- =====================================================================

alter table public.itens add column if not exists controla_lote boolean not null default false;

create table if not exists public.lotes (
  id                 uuid primary key default gen_random_uuid(),
  item_id            uuid not null references public.itens(id) on delete cascade,
  codigo_lote        text not null,
  data_validade      date,
  quantidade_inicial numeric(14,3) not null default 0,
  quantidade_atual   numeric(14,3) not null default 0,
  custo_unitario     numeric(14,2) not null default 0,
  fornecedor_id      uuid references public.fornecedores(id) on delete set null,
  criado_em          timestamptz not null default now()
);
create index if not exists idx_lotes_item on public.lotes(item_id);
create index if not exists idx_lotes_validade on public.lotes(data_validade);

alter table public.movimentacoes add column if not exists lote_id uuid references public.lotes(id) on delete set null;

-- Ajusta saldo do lote quando a movimentação referencia um lote
create or replace function public.fn_ajustar_lote()
returns trigger
language plpgsql security definer set search_path = public
as $$
declare v_atual numeric(14,3);
begin
  if new.lote_id is null then return new; end if;
  select quantidade_atual into v_atual from public.lotes where id = new.lote_id for update;
  if new.tipo = 'entrada' then
    v_atual := v_atual + new.quantidade;
  elsif new.tipo = 'saida' then
    if v_atual - new.quantidade < 0 then
      raise exception 'Saldo do lote insuficiente: disponivel %, solicitado %', v_atual, new.quantidade;
    end if;
    v_atual := v_atual - new.quantidade;
  end if;
  update public.lotes set quantidade_atual = v_atual where id = new.lote_id;
  return new;
end;
$$;

drop trigger if exists trg_ajustar_lote on public.movimentacoes;
create trigger trg_ajustar_lote
  after insert on public.movimentacoes
  for each row execute function public.fn_ajustar_lote();

revoke execute on function public.fn_ajustar_lote() from public, anon, authenticated;

alter table public.lotes enable row level security;
drop policy if exists lotes_select on public.lotes;
create policy lotes_select on public.lotes for select using (auth.uid() is not null);
drop policy if exists lotes_write on public.lotes;
create policy lotes_write on public.lotes for all
  using (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]))
  with check (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]));

-- Fornecedores B2B (CNPJ) e preço por lote no catálogo
alter table public.fornecedores add column if not exists fornece_cnpj boolean not null default true;
alter table public.fornecedor_itens add column if not exists quantidade_lote numeric(14,3) not null default 1;
alter table public.fornecedor_itens add column if not exists prazo_entrega_dias int;

-- gerar_alertas_validade passa a considerar também a validade por lote (ver 0012 no banco).
