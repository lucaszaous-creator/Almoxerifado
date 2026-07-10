-- Status "parcial" para recebimento de compras e atendimento de requisições.
alter type status_pedido add value if not exists 'parcial';
alter type status_requisicao add value if not exists 'parcial';
