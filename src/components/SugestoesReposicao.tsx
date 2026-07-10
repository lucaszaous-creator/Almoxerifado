"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";
import { fmtNumero } from "@/lib/format";
import type { Item } from "@/lib/tipos";

/** Quantidade sugerida para repor: até o estoque máximo (ou o dobro do mínimo). */
function qtdSugerida(i: Item): number {
  const alvo = i.estoque_maximo > 0 ? i.estoque_maximo : Math.max(i.estoque_minimo * 2, i.ponto_reposicao);
  return Math.max(0, alvo - i.quantidade);
}

export default function SugestoesReposicao({
  itens,
  usuarioId,
}: {
  itens: Item[];
  usuarioId: string;
}) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const toast = useToast();
  const [selecionados, setSelecionados] = useState<Set<string>>(new Set(itens.map((i) => i.id)));
  const [gerando, setGerando] = useState(false);

  function alternar(id: string) {
    setSelecionados((s) => {
      const nova = new Set(s);
      if (nova.has(id)) nova.delete(id);
      else nova.add(id);
      return nova;
    });
  }

  async function gerar() {
    const alvo = itens.filter((i) => selecionados.has(i.id));
    if (alvo.length === 0) return toast.erro("Selecione ao menos um item.");
    setGerando(true);
    const registros = alvo.map((i) => ({
      item_id: i.id,
      descricao_item: i.nome,
      quantidade_solicitada: qtdSugerida(i) || i.estoque_minimo || 1,
      unidade: i.unidade,
      fornecedor_id: i.fornecedor_id,
      preco_estimado: i.preco_custo || 0,
      justificativa: "Reposição automática (abaixo do ponto de reposição)",
      solicitante_id: usuarioId,
    }));
    const { error } = await supabase.from("pedidos_compra").insert(registros);
    setGerando(false);
    if (error) return toast.erro(error.message);
    toast.sucesso(`${registros.length} pedido(s) de reposição gerado(s).`);
    router.refresh();
  }

  return (
    <div className="card border-amber-200 bg-amber-50/40 p-4">
      <div className="mb-3 flex items-center justify-between">
        <h2 className="font-semibold text-amber-800">🔁 Sugestões de reposição ({itens.length})</h2>
        <button className="btn-primary" onClick={gerar} disabled={gerando}>
          {gerando ? "Gerando..." : `Gerar ${selecionados.size} pedido(s)`}
        </button>
      </div>
      <div className="space-y-1">
        {itens.map((i) => (
          <label
            key={i.id}
            className="flex cursor-pointer items-center justify-between rounded-lg bg-white px-3 py-2 text-sm"
          >
            <span className="flex items-center gap-2">
              <input
                type="checkbox"
                checked={selecionados.has(i.id)}
                onChange={() => alternar(i.id)}
                className="h-4 w-4 rounded border-slate-300"
              />
              <span className="font-medium text-slate-800">{i.nome}</span>
              <span className="text-slate-400">
                (tem {fmtNumero(i.quantidade)} {i.unidade})
              </span>
            </span>
            <span className="font-medium text-amber-700">
              sugerir {fmtNumero(qtdSugerida(i) || i.estoque_minimo || 1)} {i.unidade}
            </span>
          </label>
        ))}
      </div>
    </div>
  );
}
