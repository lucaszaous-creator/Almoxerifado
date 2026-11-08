"use client";

import { useMemo, useState } from "react";
import Etiqueta, { type DadosEtiqueta } from "@/components/Etiqueta";

type ItemEtiqueta = DadosEtiqueta & { id: string };

export default function EtiquetasImpressao({ itens }: { itens: ItemEtiqueta[] }) {
  const [busca, setBusca] = useState("");
  const [qtds, setQtds] = useState<Record<string, number>>({});

  const filtrados = useMemo(() => {
    const q = busca.trim().toLowerCase();
    if (!q) return itens;
    return itens.filter((i) => `${i.nome} ${i.codigo} ${i.codigo_barras ?? ""}`.toLowerCase().includes(q));
  }, [itens, busca]);

  function setQtd(id: string, v: number) {
    setQtds((s) => ({ ...s, [id]: Math.max(0, v) }));
  }

  const paraImprimir: ItemEtiqueta[] = [];
  for (const i of itens) {
    const n = qtds[i.id] ?? 0;
    for (let k = 0; k < n; k++) paraImprimir.push(i);
  }
  const total = paraImprimir.length;

  return (
    <div className="space-y-5">
      <div className="no-print space-y-4">
        <div className="card p-4">
          <div className="mb-3 flex flex-wrap items-center justify-between gap-3">
            <input
              className="input max-w-xs"
              placeholder="Buscar item..."
              value={busca}
              onChange={(e) => setBusca(e.target.value)}
            />
            <div className="flex gap-2">
              <button
                className="btn-secondary"
                onClick={() => setQtds(Object.fromEntries(filtrados.map((i) => [i.id, 1])))}
              >
                1 de cada (filtrados)
              </button>
              <button className="btn-secondary" onClick={() => setQtds({})}>
                Limpar
              </button>
              <button className="btn-primary" disabled={total === 0} onClick={() => window.print()}>
                🖨️ Imprimir ({total})
              </button>
            </div>
          </div>

          <div className="max-h-72 space-y-1 overflow-y-auto">
            {filtrados.map((i) => (
              <div key={i.id} className="flex items-center justify-between gap-2 rounded-lg px-2 py-1.5 text-sm hover:bg-slate-50">
                <span className="min-w-0 truncate text-slate-700">
                  {i.nome} <span className="text-slate-400">· {i.codigo}</span>
                </span>
                <input
                  type="number"
                  min={0}
                  className="input w-20 py-1"
                  value={qtds[i.id] ?? 0}
                  onChange={(e) => setQtd(i.id, Number(e.target.value))}
                />
              </div>
            ))}
            {filtrados.length === 0 && <p className="py-4 text-center text-sm text-slate-400">Nenhum item.</p>}
          </div>
        </div>
        {total === 0 && (
          <p className="text-sm text-slate-500">
            Defina a quantidade de etiquetas por item e clique em Imprimir. Dica: use &quot;1 de cada&quot;.
          </p>
        )}
      </div>

      {/* Área de impressão */}
      <div className="etiquetas-grid grid grid-cols-2 gap-2 sm:grid-cols-3 md:grid-cols-4">
        {paraImprimir.map((i, k) => (
          <Etiqueta key={k} item={i} />
        ))}
      </div>
    </div>
  );
}
