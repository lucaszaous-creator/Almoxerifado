-- =====================================================================
-- Custo médio ponderado + auditoria
-- =====================================================================

-- Recalcula custo médio ponderado nas entradas + aplica movimentação no estoque
create or replace function public.aplicar_movimentacao()
returns trigger
language plpgsql security definer set search_path = public
as $$
declare
  v_atual numeric(14,3);
  v_custo_medio numeric(14,2);
  v_preco numeric(14,2);
  v_custo_ent numeric(14,2);
  v_base numeric(14,2);
begin
  select quantidade, custo_medio, preco_custo
    into v_atual, v_custo_medio, v_preco
    from public.itens where id = new.item_id for update;

  if new.tipo = 'entrada' then
    v_custo_ent := coalesce(new.custo_unitario, v_preco, 0);
    v_base := case when v_atual > 0 and v_custo_medio > 0 then v_custo_medio else v_custo_ent end;
    if (v_atual + new.quantidade) > 0 then
      v_custo_medio := round(((v_atual * v_base) + (new.quantidade * v_custo_ent)) / (v_atual + new.quantidade), 2);
    else
      v_custo_medio := v_custo_ent;
    end if;
    v_atual := v_atual + new.quantidade;
  elsif new.tipo = 'saida' then
    if v_atual - new.quantidade < 0 then
      raise exception 'Estoque insuficiente: disponivel %, solicitado %', v_atual, new.quantidade;
    end if;
    v_atual := v_atual - new.quantidade;
  elsif new.tipo = 'ajuste' then
    v_atual := new.quantidade;
  end if;

  update public.itens
    set quantidade = v_atual, custo_medio = v_custo_medio, atualizado_em = now()
    where id = new.item_id;

  return new;
end;
$$;

-- Log de auditoria genérico
create or replace function public.fn_auditoria()
returns trigger
language plpgsql security definer set search_path = public
as $$
declare
  v_id uuid;
  v_desc text;
begin
  if tg_op = 'DELETE' then v_id := old.id; else v_id := new.id; end if;
  v_desc := tg_table_name || ' ' || lower(tg_op);

  if tg_table_name = 'itens' then
    v_desc := 'Item "' || coalesce(new.nome, old.nome) || '" ' ||
      case tg_op when 'INSERT' then 'cadastrado' when 'UPDATE' then 'editado' else 'excluido' end;
  elsif tg_table_name = 'fornecedores' then
    v_desc := 'Fornecedor "' || coalesce(new.nome, old.nome) || '" ' ||
      case tg_op when 'INSERT' then 'cadastrado' when 'UPDATE' then 'editado' else 'excluido' end;
  elsif tg_table_name = 'pedidos_compra' then
    v_desc := 'Pedido de compra ' ||
      case tg_op when 'INSERT' then 'criado' when 'UPDATE' then 'atualizado (' || new.status || ')' else 'excluido' end;
  elsif tg_table_name = 'requisicoes' then
    v_desc := 'Requisição ' ||
      case tg_op when 'INSERT' then 'criada' when 'UPDATE' then 'atualizada (' || new.status || ')' else 'excluida' end;
  end if;

  insert into public.auditoria (usuario_id, acao, entidade, entidade_id, descricao)
  values (auth.uid(), tg_op, tg_table_name, v_id, v_desc);

  if tg_op = 'DELETE' then return old; end if;
  return new;
end;
$$;

drop trigger if exists trg_aud_itens on public.itens;
create trigger trg_aud_itens after insert or update or delete on public.itens
  for each row execute function public.fn_auditoria();

drop trigger if exists trg_aud_fornecedores on public.fornecedores;
create trigger trg_aud_fornecedores after insert or update or delete on public.fornecedores
  for each row execute function public.fn_auditoria();

drop trigger if exists trg_aud_pedidos on public.pedidos_compra;
create trigger trg_aud_pedidos after insert or update or delete on public.pedidos_compra
  for each row execute function public.fn_auditoria();

drop trigger if exists trg_aud_requisicoes on public.requisicoes;
create trigger trg_aud_requisicoes after insert or update or delete on public.requisicoes
  for each row execute function public.fn_auditoria();

revoke execute on function public.fn_auditoria() from public, anon, authenticated;
