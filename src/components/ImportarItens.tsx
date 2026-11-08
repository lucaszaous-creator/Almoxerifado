"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";
import { baixarCsv } from "@/lib/csv";
import type { Categoria } from "@/lib/tipos";

const COLUNAS = ["nome", "codigo", "categoria", "unidade", "localizacao", "quantidade", "estoque_minimo", "preco_custo"];

export default function ImportarItens({
  categorias,
  usuarioId,
}: {
  categorias: Categoria[];
  usuarioId: string;
}) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const toast = useToast();
  const [processando, setProcessando] = useState(false);
  const [resultado, setResultado] = useState<{ ok: number; erros: string[] } | null>(null);

  function baixarModelo() {
    baixarCsv("modelo-itens.csv", COLUNAS, [
      ["Parafuso 6mm", "PAR-6MM", "Ferramentas", "un", "Prateleira A1", "100", "20", "0,50"],
      ["Caneta azul", "", "Material de escritorio", "cx", "Armário 2", "10", "5", "12,90"],
    ]);
  }

  async function processar(e: React.ChangeEvent<HTMLInputElement>) {
    const arquivo = e.target.files?.[0];
    if (!arquivo) return;
    setProcessando(true);
    setResultado(null);

    const texto = await arquivo.text();
    const linhas = texto.split(/\r?\n/).filter((l) => l.trim());
    if (linhas.length < 2) {
      toast.erro("Arquivo vazio ou sem dados.");
      setProcessando(false);
      return;
    }
    const delim = linhas[0].includes(";") ? ";" : ",";
    const cabecalho = linhas[0].split(delim).map((c) => c.trim().toLowerCase());
    const idx = (nome: string) => cabecalho.indexOf(nome);

    const mapaCat = new Map(categorias.map((c) => [c.nome.toLowerCase(), c.id]));
    const erros: string[] = [];
    let ok = 0;

    for (let n = 1; n < linhas.length; n++) {
      const cols = linhas[n].split(delim).map((c) => c.trim().replace(/^"|"$/g, ""));
      const nome = cols[idx("nome")]?.trim();
      if (!nome) {
        erros.push(`Linha ${n + 1}: nome vazio.`);
        continue;
      }
      const num = (v: string | undefined) => Number((v ?? "").replace(".", "").replace(",", ".")) || 0;
      const catNome = cols[idx("categoria")]?.trim().toLowerCase();
      const codigo = cols[idx("codigo")]?.trim() || gerarCodigo(nome);
      const qtd = num(cols[idx("quantidade")]);

      const { data: novo, error } = await supabase
        .from("itens")
        .insert({
          codigo,
          nome,
          categoria_id: catNome ? mapaCat.get(catNome) ?? null : null,
          unidade: cols[idx("unidade")]?.trim() || "un",
          localizacao: cols[idx("localizacao")]?.trim() || null,
          estoque_minimo: num(cols[idx("estoque_minimo")]),
          preco_custo: num(cols[idx("preco_custo")]),
          criado_por: usuarioId,
        })
        .select()
        .single();

      if (error) {
        erros.push(`Linha ${n + 1} (${nome}): ${error.message.includes("duplicate") ? "código duplicado" : error.message}`);
        continue;
      }
      if (qtd > 0 && novo) {
        await supabase.from("movimentacoes").insert({
          item_id: novo.id,
          tipo: "entrada",
          quantidade: qtd,
          motivo: "Estoque inicial",
          observacao: "Importação CSV",
          usuario_id: usuarioId,
        });
      }
      ok += 1;
    }

    setProcessando(false);
    setResultado({ ok, erros });
    if (ok > 0) {
      toast.sucesso(`${ok} item(ns) importado(s).`);
      router.refresh();
    }
    e.target.value = "";
  }

  return (
    <div className="space-y-4">
      <div className="card p-6">
        <h2 className="font-semibold text-slate-800">Como importar</h2>
        <ol className="mt-2 list-inside list-decimal space-y-1 text-sm text-slate-600">
          <li>Baixe o modelo e preencha uma linha por item.</li>
          <li>Colunas: {COLUNAS.join(", ")}.</li>
          <li>A categoria deve ter o mesmo nome de uma já cadastrada (senão fica em branco).</li>
          <li>A quantidade informada entra como estoque inicial.</li>
        </ol>
        <div className="mt-4 flex flex-wrap gap-3">
          <button onClick={baixarModelo} className="btn-secondary">
            ⬇️ Baixar modelo CSV
          </button>
          <label className="btn-primary cursor-pointer">
            {processando ? "Processando..." : "Selecionar arquivo CSV"}
            <input type="file" accept=".csv,text/csv" className="hidden" onChange={processar} disabled={processando} />
          </label>
        </div>
      </div>

      {resultado && (
        <div className="card p-6">
          <p className="font-medium text-emerald-700">✅ {resultado.ok} item(ns) importado(s) com sucesso.</p>
          {resultado.erros.length > 0 && (
            <div className="mt-3">
              <p className="text-sm font-medium text-rose-700">{resultado.erros.length} com problema:</p>
              <ul className="mt-1 max-h-48 space-y-1 overflow-y-auto text-sm text-slate-500">
                {resultado.erros.map((er, k) => (
                  <li key={k}>• {er}</li>
                ))}
              </ul>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

function gerarCodigo(nome: string): string {
  const base = nome.toUpperCase().replace(/[^A-Z0-9]/g, "").slice(0, 4);
  return `${base || "ITEM"}-${Math.floor(1000 + Math.random() * 9000)}`;
}
