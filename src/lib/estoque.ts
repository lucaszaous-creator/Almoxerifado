/**
 * Sugestões inteligentes de parâmetros de estoque a partir do consumo médio.
 * Padrões conservadores: 7 dias de prazo de entrega + 3 dias de segurança;
 * estoque máximo cobrindo ~30 dias de consumo.
 */
export function pontoReposicaoSugerido(consumoDia: number, prazoDias = 7, segurancaDias = 3): number {
  if (consumoDia <= 0) return 0;
  return Math.ceil(consumoDia * (prazoDias + segurancaDias));
}

export function estoqueMaximoSugerido(consumoDia: number, cicloDias = 30): number {
  if (consumoDia <= 0) return 0;
  return Math.ceil(consumoDia * cicloDias);
}

/** Quantidade sugerida de compra para repor até o alvo (máx. ou ponto). */
export function quantidadeReposicao(quantidadeAtual: number, alvo: number): number {
  return Math.max(0, Math.ceil(alvo - quantidadeAtual));
}
