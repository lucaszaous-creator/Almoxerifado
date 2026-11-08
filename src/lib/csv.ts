function escaparCampo(valor: unknown): string {
  const s = valor === null || valor === undefined ? "" : String(valor);
  if (/[";\n]/.test(s)) return `"${s.replace(/"/g, '""')}"`;
  return s;
}

/** Gera CSV (separador ";" para compatibilidade com Excel pt-BR) e dispara o download. */
export function baixarCsv(nomeArquivo: string, colunas: string[], linhas: unknown[][]) {
  const conteudo = [
    colunas.map(escaparCampo).join(";"),
    ...linhas.map((l) => l.map(escaparCampo).join(";")),
  ].join("\r\n");

  // BOM p/ o Excel reconhecer acentuação UTF-8
  const blob = new Blob(["﻿" + conteudo], { type: "text/csv;charset=utf-8;" });
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = nomeArquivo;
  document.body.appendChild(a);
  a.click();
  document.body.removeChild(a);
  URL.revokeObjectURL(url);
}
