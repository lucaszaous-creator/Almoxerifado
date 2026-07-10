-- =====================================================================
-- Hardening de segurança: restringe EXECUTE das funções SECURITY DEFINER
-- =====================================================================
-- Sem isto, funções como notificar_papeis ficariam expostas via
-- /rest/v1/rpc para qualquer usuário logado, permitindo inserir
-- notificações arbitrárias. Funções de trigger continuam executando
-- normalmente (o disparo do trigger não checa o EXECUTE do usuário).

revoke execute on function public.meu_papel() from public, anon, authenticated;
revoke execute on function public.tem_papel(papel_usuario[]) from public, anon, authenticated;
revoke execute on function public.notificar_papeis(papel_usuario[], tipo_notificacao, text, text, uuid, uuid) from public, anon, authenticated;
revoke execute on function public.gerar_alertas_validade(int) from public, anon, authenticated;
revoke execute on function public.handle_new_user() from public, anon, authenticated;
revoke execute on function public.aplicar_movimentacao() from public, anon, authenticated;
revoke execute on function public.verificar_estoque_baixo() from public, anon, authenticated;
revoke execute on function public.notificar_novo_pedido() from public, anon, authenticated;
revoke execute on function public.notificar_status_pedido() from public, anon, authenticated;

-- Concede apenas o necessário:
--  - tem_papel / meu_papel: usadas nas policies de RLS (avaliadas como o usuário logado)
--  - gerar_alertas_validade: chamada pelo app (painel) para gerar alertas de validade
grant execute on function public.meu_papel() to authenticated;
grant execute on function public.tem_papel(papel_usuario[]) to authenticated;
grant execute on function public.gerar_alertas_validade(int) to authenticated;
