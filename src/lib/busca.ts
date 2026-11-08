/** Utilitários para pesquisar preços de um produto na web. */

export type DadosProduto = {
  nome: string;
  marca?: string | null;
  fabricante?: string | null;
  codigo_barras?: string | null;
};

/** Monta uma consulta de pesquisa a partir dos dados do produto. */
export function montarConsulta(p: DadosProduto, b2b: boolean): string {
  const partes = [p.nome, p.marca ?? "", p.fabricante ?? ""].map((s) => s.trim()).filter(Boolean);
  let q = partes.join(" ");
  // Prioriza o código de barras quando disponível (busca mais exata)
  if (p.codigo_barras && p.codigo_barras.trim()) {
    q = `${p.codigo_barras.trim()} ${q}`.trim();
  }
  q += b2b ? " preço atacado distribuidor CNPJ nota fiscal" : " preço comprar";
  return q.trim();
}

export function urlGoogle(consulta: string): string {
  return `https://www.google.com/search?hl=pt-BR&gl=br&q=${encodeURIComponent(consulta)}`;
}

export function urlGoogleShopping(consulta: string): string {
  return `https://www.google.com/search?tbm=shop&hl=pt-BR&gl=br&q=${encodeURIComponent(consulta)}`;
}

export function urlMercadoLivre(consulta: string): string {
  return `https://lista.mercadolivre.com.br/${encodeURIComponent(consulta.replace(/\s+/g, "-"))}`;
}
