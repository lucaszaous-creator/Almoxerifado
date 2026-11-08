-- Rotina diária automática: gera alertas de validade e um resumo de reposição,
-- mesmo que ninguém abra o app.
create or replace function public.job_diario()
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_dias int;
  v_repor int;
begin
  select dias_alerta_validade into v_dias from public.configuracoes where id = true;
  perform public.gerar_alertas_validade(coalesce(v_dias, 30));

  select count(*) into v_repor
  from public.itens
  where ativo and greatest(ponto_reposicao, estoque_minimo) > 0
    and quantidade <= greatest(ponto_reposicao, estoque_minimo);

  if v_repor > 0 and not exists (
    select 1 from public.notificacoes
    where tipo = 'estoque_baixo' and titulo = 'Resumo de reposição'
      and criado_em > now() - interval '20 hours'
  ) then
    perform public.notificar_papeis(
      array['gerente','almoxarife']::papel_usuario[],
      'estoque_baixo', 'Resumo de reposição',
      v_repor || ' item(ns) estão no ponto de reposição. Veja em Compras > sugestões de reposição.',
      null, null
    );
  end if;
end;
$$;

revoke execute on function public.job_diario() from public, anon, authenticated;

-- Agenda diária às 09:00 UTC (06:00 BRT). cron.schedule faz upsert por nome.
select cron.schedule('almox_job_diario', '0 9 * * *', $$ select public.job_diario(); $$);
