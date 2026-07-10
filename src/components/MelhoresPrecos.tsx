"use client";

import { useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";
import { fmtMoeda, fmtNumero } from "@/lib/format";
import PesquisaPrecos from "@/components/PesquisaPrecos";
import type { FornecedorItem } from "@/lib/tipos";

type Oferta = FornecedorItem & { unit: number };
type Grupo = {
  itemId: string;
  nome: string;
  codigo: string;
  unidade: string;
  quantidade: number;
  precisaRepor: boolean;
  ofertas: Oferta[];
};

export default function MelhoresPrecos({
  precos,
  usuarioId,
}: {
  precos: FornecedorItem[];
  usuarioId: string;
}) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const toast = useToast();
  const [busca, setBusca] = useState("");
  const [soCnpj, setSoCnpj] = useState(true);
  const [soRepor, setSoRepor] = useState(false);
  const [ocupado, setOcupado] = useState<string | null>(null);

  const grupos = useMemo(() => {
    const map = new Map<string, Grupo>();
    for (const p of precos) {
      if (!p.itens || p.itens && (p.itens as { ativo?: boolean }).ativo === false) continue;
      if (soCnpj && p.fornecedores?.fornece_cnpj === false) continue;
      const it = p.itens as { nome: string; codigo: string; unidade: string; quantidade: number; estoque_minimo: number; ponto_reposicao: number };
      const unit = p.quantidade_lote > 0 ? p.preco / p.quantidade_lote : p.preco;
      const g =
        map.get(p.item_id) ??
        {
          itemId: p.item_id,
          nome: it.nome,
          codigo: it.codigo,
          unidade: it.unidade,
          quantidade: it.quantidade,
          precisaRepor:
            Math.max(it.ponto_reposicao || 0, it.estoque_minimo || 0) > 0 &&
            it.quantidade <= Math.max(it.ponto_reposicao || 0, it.estoque_minimo || 0),
          ofertas: [],
        };
      g.ofertas.push({ ...p, unit });
      map.set(p.item_id, g);
    }
    let lista = [...map.values()];
    lista.forEach((g) => g.ofertas.sort((a, b) => a.unit - b.unit));
    const q = busca.trim().toLowerCase();
    if (q) lista = lista.filter((g) => `${g.nome} ${g.codigo}`.toLowerCase().includes(q));
    if (soRepor) lista = lista.filter((g) => g.precisaRepor);
    return lista.sort((a, b) => a.nome.localeCompare(b.nome));
  }, [precos, busca, soCnpj, soRepor]);

  async function gerarPedido(g: Grupo, oferta: Oferta) {
    setOcupado(g.itemId);
    const { error } = await supabase.from("pedidos_compra").insert({
      item_id: g.itemId,
      descricao_item: g.nome,
      quantidade_solicitada: oferta.quantidade_lote || 1,
      unidade: g.unidade,
      fornecedor_id: oferta.fornecedor_id,
      preco_estimado: oferta.unit,
      justificativa: `Melhor preço CNPJ (${fmtMoeda(oferta.unit)}/un via ${oferta.fornecedores?.nome ?? "fornecedor"})`,
      solicitante_id: usuarioId,
    });
    setOcupado(null);
    if (error) return toast.erro(error.message);
    toast.sucesso("Pedido gerado com o fornecedor de melhor preço.");
    router.refresh();
  }

  return (
    <div className="space-y-4">
      <div className="card flex flex-col gap-3 p-4 sm:flex-row sm:items-center">
        <div className="relative flex-1">
          <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">🔍</span>
          <input
            className="input pl-9"
            placeholder="Buscar produto por nome ou código..."
            value={busca}
            onChange={(e) => setBusca(e.target.value)}
          />
        </div>
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" checked={soCnpj} onChange={(e) => setSoCnpj(e.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Somente CNPJ
        </label>
        <label className="flex items-center gap-2 text-sm text-slate-600">
          <input type="checkbox" checked={soRepor} onChange={(e) => setSoRepor(e.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          Só a repor
        </label>
      </div>

      {busca.trim().length >= 2 && (
        <div>
          <p className="mb-2 text-sm text-slate-500">
            Pesquise <span className="font-medium">“{busca.trim()}”</span> na internet para achar
            fornecedores e preços além dos cadastrados:
          </p>
          <PesquisaPrecos produto={{ nome: busca }} />
        </div>
      )}

      {grupos.length === 0 && (
        <div className="card p-10 text-center text-slate-400">
          Nenhum preço cadastrado. Registre preços de fornecedores na página do item (&quot;Preços por
          fornecedor&quot;).
        </div>
      )}

      {grupos.map((g) => {
        const melhor = g.ofertas[0];
        return (
          <div key={g.itemId} className="card p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p className="font-semibold text-slate-800">
                  {g.nome}
                  {g.precisaRepor && <span className="badge ml-2 bg-amber-100 text-amber-700">repor</span>}
                </p>
                <p className="text-xs text-slate-400">
                  cód. {g.codigo} · em estoque {fmtNumero(g.quantidade)} {g.unidade}
                </p>
              </div>
              {melhor && (
                <div className="text-right">
                  <p className="text-lg font-bold text-emerald-700">{fmtMoeda(melhor.unit)}/un</p>
                  <p className="text-xs text-slate-400">melhor: {melhor.fornecedores?.nome}</p>
                </div>
              )}
            </div>

            <div className="mt-3 space-y-1">
              {g.ofertas.map((o, k) => (
                <div
                  key={o.id}
                  className={`flex items-center justify-between gap-2 rounded-lg px-3 py-2 text-sm ${k === 0 ? "bg-emerald-50" : "bg-slate-50"}`}
                >
                  <span className="min-w-0 truncate text-slate-700">
                    {o.fornecedores?.nome ?? "—"}
                    <span className="text-slate-400">
                      {" · "}lote {o.quantidade_lote} {g.unidade}
                      {o.prazo_entrega_dias ? ` · entrega ${o.prazo_entrega_dias}d` : ""}
                    </span>
                  </span>
                  <span className="flex shrink-0 items-center gap-3">
                    <span className="text-right">
                      <span className={`block font-medium ${k === 0 ? "text-emerald-700" : "text-slate-700"}`}>
                        {fmtMoeda(o.unit)}/un
                      </span>
                      <span className="text-xs text-slate-400">{fmtMoeda(o.preco)} / lote</span>
                    </span>
                    <button
                      className="btn-secondary py-1"
                      disabled={ocupado === g.itemId}
                      onClick={() => gerarPedido(g, o)}
                    >
                      Pedir
                    </button>
                  </span>
                </div>
              ))}
            </div>
          </div>
        );
      })}
    </div>
  );
}
