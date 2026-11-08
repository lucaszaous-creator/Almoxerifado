-- =====================================================================
-- Custo de itens, fornecedores e imagens
-- =====================================================================

create table if not exists public.fornecedores (
  id         uuid primary key default gen_random_uuid(),
  nome       text not null unique,
  cnpj       text,
  contato    text,
  telefone   text,
  email      text,
  observacao text,
  criado_em  timestamptz not null default now()
);

alter table public.itens add column if not exists preco_custo numeric(14,2) not null default 0;
alter table public.itens add column if not exists fornecedor_id uuid references public.fornecedores(id) on delete set null;
alter table public.itens add column if not exists imagem_url text;

alter table public.pedidos_compra add column if not exists fornecedor_id uuid references public.fornecedores(id) on delete set null;

alter table public.fornecedores enable row level security;

drop policy if exists fornecedores_select on public.fornecedores;
create policy fornecedores_select on public.fornecedores
  for select using (auth.uid() is not null);

drop policy if exists fornecedores_write on public.fornecedores;
create policy fornecedores_write on public.fornecedores
  for all using (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]))
  with check (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]));

-- Storage: bucket público para imagens de itens
insert into storage.buckets (id, name, public)
values ('itens', 'itens', true)
on conflict (id) do nothing;

drop policy if exists itens_img_read on storage.objects;
create policy itens_img_read on storage.objects
  for select using (bucket_id = 'itens');

drop policy if exists itens_img_insert on storage.objects;
create policy itens_img_insert on storage.objects
  for insert to authenticated with check (bucket_id = 'itens');

drop policy if exists itens_img_update on storage.objects;
create policy itens_img_update on storage.objects
  for update to authenticated using (bucket_id = 'itens');

drop policy if exists itens_img_delete on storage.objects;
create policy itens_img_delete on storage.objects
  for delete to authenticated using (bucket_id = 'itens');
