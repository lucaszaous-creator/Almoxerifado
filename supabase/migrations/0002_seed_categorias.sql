-- Categorias iniciais (podem ser editadas/removidas no app)
insert into public.categorias (nome, descricao) values
  ('Material de escritório', 'Papelaria, canetas, papel, etc.'),
  ('Limpeza', 'Produtos e materiais de limpeza'),
  ('Ferramentas', 'Ferramentas manuais e elétricas'),
  ('EPI', 'Equipamentos de proteção individual'),
  ('Elétrica', 'Materiais elétricos'),
  ('Hidráulica', 'Materiais hidráulicos'),
  ('Manutenção', 'Peças e insumos de manutenção'),
  ('Informática', 'Suprimentos e periféricos de TI')
on conflict (nome) do nothing;
