"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";
import { fmtMoeda } from "@/lib/format";
import type { Fornecedor, FornecedorItem } from "@/lib/tipos";

export default function PrecosFornecedor({
  itemId,
  fornecedores,
  precos,
}: {
  itemId: string;
  fornecedores: Fornecedor[];
  precos: FornecedorItem[];
}) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const toast = useToast();
  const [fornecedorId, setFornecedorId] = useState("");
  const [preco, setPreco] = useState<number>(0);
  const [qtdLote, setQtdLote] = useState<number>(1);
  const [prazo, setPrazo] = useState<number>(0);
  const [codigo, setCodigo] = useState("");
  const [salvando, setSalvando] = useState(false);

  const unit = (p: { preco: number; quantidade_lote: number }) =>
    p.quantidade_lote > 0 ? p.preco / p.quantidade_lote : p.preco;
  const melhor = precos.length ? Math.min(...precos.map((p) => unit(p))) : null;

  async function adicionar(e: React.FormEvent) {
    e.preventDefault();
    if (!fornecedorId) return toast.erro("Selecione um fornecedor.");
    setSalvando(true);
    const { error } = await supabase.from("fornecedor_itens").upsert(
      {
        fornecedor_id: fornecedorId,
        item_id: itemId,
        preco,
        quantidade_lote: qtdLote || 1,
        prazo_entrega_dias: prazo || null,
        codigo_fornecedor: codigo.trim() || null,
      },
      { onConflict: "fornecedor_id,item_id" }
    );
    setSalvando(false);
    if (error) return toast.erro(error.message);
    toast.sucesso("Preço registrado.");
    setFornecedorId("");
    setPreco(0);
    setQtdLote(1);
    setPrazo(0);
    setCodigo("");
    router.refresh();
  }

  async function remover(id: string) {
    await supabase.from("fornecedor_itens").delete().eq("id", id);
    toast.sucesso("Preço removido.");
    router.refresh();
  }

  return (
    <div className="card p-6">
      <h2 className="mb-3 font-semibold text-slate-800">Preços por fornecedor (cotações)</h2>

      {precos.length > 0 ? (
        <div className="mb-4 space-y-1">
          {precos
            .slice()
            .sort((a, b) => unit(a) - unit(b))
            .map((p) => {
              const ehMelhor = melhor !== null && Math.abs(unit(p) - melhor) < 0.0001;
              return (
                <div key={p.id} className="flex items-center justify-between rounded-lg bg-slate-50 px-3 py-2 text-sm">
                  <span className="text-slate-700">
                    {p.fornecedores?.nome ?? "—"}
                    {p.fornecedores?.fornece_cnpj === false && (
                      <span className="ml-1 text-xs text-rose-500">(não CNPJ)</span>
                    )}
                    <span className="text-slate-400">
                      {" · "}lote {p.quantidade_lote}
                      {p.prazo_entrega_dias ? ` · ${p.prazo_entrega_dias}d` : ""}
                      {p.codigo_fornecedor ? ` · ${p.codigo_fornecedor}` : ""}
                    </span>
                  </span>
                  <span className="flex items-center gap-3">
                    <span className="text-right">
                      <span className={`block font-medium ${ehMelhor ? "text-emerald-700" : "text-slate-700"}`}>
                        {fmtMoeda(unit(p))}/un
                        {ehMelhor && <span className="ml-1 text-xs">(melhor)</span>}
                      </span>
                      <span className="text-xs text-slate-400">{fmtMoeda(p.preco)} / lote</span>
                    </span>
                    <button onClick={() => remover(p.id)} className="text-slate-300 hover:text-rose-600">
                      ✕
                    </button>
                  </span>
                </div>
              );
            })}
        </div>
      ) : (
        <p className="mb-4 text-sm text-slate-400">Nenhum preço de fornecedor cadastrado.</p>
      )}

      <form onSubmit={adicionar} className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6 lg:items-end">
        <div className="col-span-2 sm:col-span-3 lg:col-span-2">
          <label className="label">Fornecedor</label>
          <select className="input" value={fornecedorId} onChange={(e) => setFornecedorId(e.target.value)}>
            <option value="">Selecione...</option>
            {fornecedores.map((f) => (
              <option key={f.id} value={f.id}>
                {f.nome}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label className="label">Preço do lote</label>
          <input className="input" type="number" min={0} step="0.01" value={preco} onChange={(e) => setPreco(Number(e.target.value))} />
        </div>
        <div>
          <label className="label">Qtd/lote</label>
          <input className="input" type="number" min={1} step="any" value={qtdLote} onChange={(e) => setQtdLote(Number(e.target.value))} />
        </div>
        <div>
          <label className="label">Prazo (d)</label>
          <input className="input" type="number" min={0} value={prazo} onChange={(e) => setPrazo(Number(e.target.value))} />
        </div>
        <div>
          <label className="label">Cód. fornec.</label>
          <input className="input" value={codigo} onChange={(e) => setCodigo(e.target.value)} />
        </div>
        <button type="submit" className="btn-secondary col-span-2 sm:col-span-3 lg:col-span-6" disabled={salvando}>
          Adicionar preço
        </button>
      </form>
    </div>
  );
}
