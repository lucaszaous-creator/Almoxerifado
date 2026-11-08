-- A baixa de estoque das requisições passa a ser controlada pelo app
-- (para permitir atendimento parcial por item). O trigger agora só notifica.
create or replace function public.processar_status_requisicao()
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

  if new.status = 'aprovada' then
    v_tipo := 'requisicao_aprovada'; v_titulo := 'Requisição aprovada';
  elsif new.status = 'rejeitada' then
    v_tipo := 'requisicao_rejeitada'; v_titulo := 'Requisição rejeitada';
  elsif new.status in ('atendida','parcial') then
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

-- Permite ao gestor atualizar quantidade_atendida nos itens da requisição
drop policy if exists reqitens_update on public.requisicao_itens;
create policy reqitens_update on public.requisicao_itens
  for update using (public.tem_papel(array['gerente','almoxarife']::papel_usuario[]));
