/**
 * Remove caracteres que quebram a sintaxe do filtro `.or()` do PostgREST
 * (vírgulas, parênteses, curingas), evitando erros ou buscas malformadas.
 */
export function sanitizarBusca(termo: string): string {
  return termo.replace(/[%,()\\*]/g, " ").replace(/\s+/g, " ").trim();
}

/** Sanitiza um código (barras/SKU) para uso seguro em filtros `eq`. */
export function sanitizarCodigo(codigo: string): string {
  return codigo.replace(/[,()]/g, "").trim();
}
