"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";
import { fmtNumero, fmtMoeda, fmtData, diasAteValidade } from "@/lib/format";
import type { Fornecedor, Item, Lote } from "@/lib/tipos";

export default function GestaoLotes({
  item,
  lotes,
  fornecedores,
  usuarioId,
}: {
  item: Item;
  lotes: Lote[];
  fornecedores: Fornecedor[];
  usuarioId: string;
}) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const toast = useToast();

  const [codigo, setCodigo] = useState("");
  const [validade, setValidade] = useState("");
  const [qtd, setQtd] = useState<number>(0);
  const [custo, setCusto] = useState<number>(item.preco_custo ?? 0);
  const [fornecedorId, setFornecedorId] = useState(item.fornecedor_id ?? "");
  const [saida, setSaida] = useState<number>(0);
  const [salvando, setSalvando] = useState(false);

  const ordenados = [...lotes].sort((a, b) => {
    if (!a.data_validade) return 1;
    if (!b.data_validade) return -1;
    return a.data_validade.localeCompare(b.data_validade);
  });
  const comSaldo = ordenados.filter((l) => l.quantidade_atual > 0);

  async function adicionarLote(e: React.FormEvent) {
    e.preventDefault();
    if (!codigo.trim()) return toast.erro("Informe o código do lote.");
    if (qtd <= 0) return toast.erro("Informe a quantidade.");
    setSalvando(true);
    const { data: lote, error } = await supabase
      .from("lotes")
      .insert({
        item_id: item.id,
        codigo_lote: codigo.trim(),
        data_validade: validade || null,
        quantidade_inicial: qtd,
        custo_unitario: custo,
        fornecedor_id: fornecedorId || null,
      })
      .select()
      .single();
    if (error || !lote) {
      setSalvando(false);
      return toast.erro(error?.message ?? "Erro ao criar lote.");
    }
    const { error: e2 } = await supabase.from("movimentacoes").insert({
      item_id: item.id,
      tipo: "entrada",
      quantidade: qtd,
      motivo: "Compra",
      custo_unitario: custo,
      lote_id: lote.id,
      observacao: `Entrada do lote ${codigo.trim()}`,
      usuario_id: usuarioId,
    });
    setSalvando(false);
    if (e2) return toast.erro(e2.message);
    toast.sucesso("Lote registrado e estoque atualizado.");
    setCodigo("");
    setValidade("");
    setQtd(0);
    router.refresh();
  }

  async function darSaidaFefo(e: React.FormEvent) {
    e.preventDefault();
    let restante = saida;
    if (restante <= 0) return toast.erro("Informe a quantidade.");
    const disponivel = comSaldo.reduce((s, l) => s + l.quantidade_atual, 0);
    if (restante > disponivel) return toast.erro("Saldo insuficiente nos lotes.");
    setSalvando(true);
    for (const l of comSaldo) {
      if (restante <= 0) break;
      const usar = Math.min(restante, l.quantidade_atual);
      const { error } = await supabase.from("movimentacoes").insert({
        item_id: item.id,
        tipo: "saida",
        quantidade: usar,
        motivo: "Consumo",
        lote_id: l.id,
        observacao: `Saída FEFO do lote ${l.codigo_lote}`,
        usuario_id: usuarioId,
      });
      if (error) {
        setSalvando(false);
        return toast.erro(error.message);
      }
      restante -= usar;
    }
    setSalvando(false);
    toast.sucesso("Saída registrada (FEFO — vence primeiro, sai primeiro).");
    setSaida(0);
    router.refresh();
  }

  return (
    <div className="card p-6">
      <h2 className="mb-1 font-semibold text-slate-800">Lotes (validade por lote)</h2>
      <p className="mb-4 text-sm text-slate-500">Saída automática por FEFO: o lote que vence primeiro sai primeiro.</p>

      <div className="mb-4 overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-slate-50">
            <tr>
              <th className="th">Lote</th>
              <th className="th">Validade</th>
              <th className="th text-right">Saldo</th>
              <th className="th text-right">Custo</th>
              <th className="th">Fornecedor</th>
            </tr>
          </thead>
          <tbody>
            {ordenados.map((l) => {
              const d = diasAteValidade(l.data_validade);
              return (
                <tr key={l.id} className="border-t border-slate-100">
                  <td className="px-4 py-2 font-medium text-slate-800">{l.codigo_lote}</td>
                  <td className="px-4 py-2">
                    {l.data_validade ? (
                      <span className={d !== null && d < 0 ? "text-rose-600" : d !== null && d <= 30 ? "text-amber-600" : "text-slate-600"}>
                        {fmtData(l.data_validade)}
                      </span>
                    ) : (
                      "—"
                    )}
                  </td>
                  <td className="px-4 py-2 text-right font-medium text-slate-800">
                    {fmtNumero(l.quantidade_atual)} {item.unidade}
                  </td>
                  <td className="px-4 py-2 text-right text-slate-600">{fmtMoeda(l.custo_unitario)}</td>
                  <td className="px-4 py-2 text-slate-600">{l.fornecedores?.nome ?? "—"}</td>
                </tr>
              );
            })}
            {ordenados.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-slate-400">
                  Nenhum lote registrado.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <form onSubmit={adicionarLote} className="space-y-3 rounded-lg border border-slate-200 p-4">
          <h3 className="text-sm font-semibold text-slate-700">Entrada de lote</h3>
          <div className="grid grid-cols-2 gap-2">
            <input className="input" placeholder="Código do lote" value={codigo} onChange={(e) => setCodigo(e.target.value)} />
            <input className="input" type="date" value={validade} onChange={(e) => setValidade(e.target.value)} />
            <input className="input" type="number" min={0} step="any" placeholder="Quantidade" value={qtd} onChange={(e) => setQtd(Number(e.target.value))} />
            <input className="input" type="number" min={0} step="0.01" placeholder="Custo unit." value={custo} onChange={(e) => setCusto(Number(e.target.value))} />
          </div>
          <select className="input" value={fornecedorId} onChange={(e) => setFornecedorId(e.target.value)}>
            <option value="">Sem fornecedor</option>
            {fornecedores.map((f) => (
              <option key={f.id} value={f.id}>
                {f.nome}
              </option>
            ))}
          </select>
          <button type="submit" className="btn-primary w-full" disabled={salvando}>
            Registrar entrada de lote
          </button>
        </form>

        <form onSubmit={darSaidaFefo} className="space-y-3 rounded-lg border border-slate-200 p-4">
          <h3 className="text-sm font-semibold text-slate-700">Saída (FEFO)</h3>
          <p className="text-xs text-slate-400">
            Disponível em lotes: {fmtNumero(comSaldo.reduce((s, l) => s + l.quantidade_atual, 0))} {item.unidade}
          </p>
          <input
            className="input"
            type="number"
            min={0}
            step="any"
            placeholder="Quantidade a retirar"
            value={saida}
            onChange={(e) => setSaida(Number(e.target.value))}
          />
          <button type="submit" className="btn-secondary w-full" disabled={salvando || comSaldo.length === 0}>
            Retirar (vence primeiro)
          </button>
        </form>
      </div>
    </div>
  );
}
