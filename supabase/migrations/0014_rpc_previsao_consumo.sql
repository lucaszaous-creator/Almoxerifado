-- Previsão de consumo: consumo diário médio (janela) e dias de cobertura por item.
create or replace function public.previsao_consumo(p_dias int default 60)
returns json
language sql
stable
set search_path = public
as $$
  select coalesce(json_agg(row_to_json(x) order by x.dias_cobertura asc nulls last), '[]'::json)
  from (
    select
      i.id as item_id,
      i.nome,
      i.unidade,
      i.quantidade,
      round(s.total / p_dias::numeric, 3) as consumo_dia,
      case when s.total > 0
        then round(i.quantidade / (s.total / p_dias::numeric), 1)
        else null end as dias_cobertura
    from public.itens i
    join (
      select item_id, sum(quantidade) as total
      from public.movimentacoes
      where tipo = 'saida' and criado_em >= now() - (p_dias || ' days')::interval
      group by item_id
    ) s on s.item_id = i.id
    where i.ativo and s.total > 0
  ) x;
$$;

grant execute on function public.previsao_consumo(int) to authenticated;

-- Consumo diário de um item específico (para a ficha).
create or replace function public.consumo_dia_item(p_item uuid, p_dias int default 60)
returns numeric
language sql
stable
set search_path = public
as $$
  select round(coalesce(sum(quantidade), 0) / p_dias::numeric, 3)
  from public.movimentacoes
  where item_id = p_item and tipo = 'saida'
    and criado_em >= now() - (p_dias || ' days')::interval;
$$;

grant execute on function public.consumo_dia_item(uuid, int) to authenticated;
