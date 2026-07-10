-- Resumo agregado de movimentações (últimos 30 dias) para o painel,
-- evitando transferir milhares de linhas para o cliente.
create or replace function public.mov_resumo_30d()
returns json
language sql
stable
set search_path = public
as $$
  select json_build_object(
    'entradas30', coalesce((select sum(quantidade) from public.movimentacoes
        where tipo = 'entrada' and criado_em >= now() - interval '30 days'), 0),
    'saidas30', coalesce((select sum(quantidade) from public.movimentacoes
        where tipo = 'saida' and criado_em >= now() - interval '30 days'), 0),
    'serie14', (
      select coalesce(json_agg(row_to_json(t) order by t.d), '[]'::json) from (
        select gs::date as d,
          coalesce((select sum(quantidade) from public.movimentacoes
              where tipo = 'entrada' and criado_em::date = gs::date), 0) as ent,
          coalesce((select sum(quantidade) from public.movimentacoes
              where tipo = 'saida' and criado_em::date = gs::date), 0) as sai
        from generate_series(current_date - 13, current_date, interval '1 day') gs
      ) t
    ),
    'top', (
      select coalesce(json_agg(row_to_json(x)), '[]'::json) from (
        select i.nome, i.unidade, sum(m.quantidade) as qtd
        from public.movimentacoes m
        join public.itens i on i.id = m.item_id
        where m.tipo = 'saida' and m.criado_em >= now() - interval '30 days'
        group by i.nome, i.unidade
        order by qtd desc
        limit 5
      ) x
    )
  );
$$;

grant execute on function public.mov_resumo_30d() to authenticated;
