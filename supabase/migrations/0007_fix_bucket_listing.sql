-- Bucket público serve imagens por URL sem necessidade de policy de SELECT.
-- Remover a policy de leitura evita que clientes listem todos os arquivos do
-- bucket (recomendação do linter de segurança do Supabase).
drop policy if exists itens_img_read on storage.objects;
