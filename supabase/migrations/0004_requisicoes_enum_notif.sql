-- Novos tipos de notificação para o fluxo de requisições de material.
-- Precisa ser aplicado em migration separada: o Postgres não permite usar
-- um novo valor de enum na mesma transação em que ele é adicionado.
alter type tipo_notificacao add value if not exists 'nova_requisicao';
alter type tipo_notificacao add value if not exists 'requisicao_aprovada';
alter type tipo_notificacao add value if not exists 'requisicao_rejeitada';
alter type tipo_notificacao add value if not exists 'requisicao_atendida';
