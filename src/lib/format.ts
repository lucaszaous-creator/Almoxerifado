export function fmtNumero(n: number): string {
  return new Intl.NumberFormat("pt-BR", { maximumFractionDigits: 3 }).format(n);
}

export function fmtMoeda(n: number): string {
  return new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" }).format(n || 0);
}

export function fmtData(iso: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso.length <= 10 ? iso + "T00:00:00" : iso);
  return d.toLocaleDateString("pt-BR");
}

export function fmtDataHora(iso: string): string {
  return new Date(iso).toLocaleString("pt-BR");
}

/** Dias até a validade (negativo = vencido). null se sem data. */
export function diasAteValidade(dataValidade: string | null): number | null {
  if (!dataValidade) return null;
  const hoje = new Date();
  hoje.setHours(0, 0, 0, 0);
  const dv = new Date(dataValidade + "T00:00:00");
  return Math.round((dv.getTime() - hoje.getTime()) / 86400000);
}
