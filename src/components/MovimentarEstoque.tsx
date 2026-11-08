"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";
import { fmtNumero } from "@/lib/format";
import { MOTIVOS_ENTRADA, MOTIVOS_SAIDA, type Item, type TipoMovimentacao } from "@/lib/tipos";

export default function MovimentarEstoque({
  item,
  usuarioId,
}: {
  item: Item;
  usuarioId: string;
}) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const toast = useToast();
  const [tipo, setTipo] = useState<TipoMovimentacao>("entrada");
  const [quantidade, setQuantidade] = useState<number>(0);
  const [motivo, setMotivo] = useState("");
  const [custoUnitario, setCustoUnitario] = useState<number>(item.preco_custo ?? 0);
  const [observacao, setObservacao] = useState("");
  const [salvando, setSalvando] = useState(false);

  const motivos = tipo === "entrada" ? MOTIVOS_ENTRADA : tipo === "saida" ? MOTIVOS_SAIDA : ["Ajuste de inventário"];

  async function registrar(e: React.FormEvent) {
    e.preventDefault();
    if (quantidade < 0) {
      toast.erro("Quantidade inválida.");
      return;
    }
    setSalvando(true);
    const { error } = await supabase.from("movimentacoes").insert({
      item_id: item.id,
      tipo,
      quantidade,
      motivo: motivo || null,
      custo_unitario: tipo === "entrada" ? custoUnitario : null,
      observacao: observacao.trim() || null,
      usuario_id: usuarioId,
    });
    setSalvando(false);
    if (error) {
      toast.erro(error.message.includes("insuficiente") ? "Estoque insuficiente para esta saída." : error.message);
      return;
    }
    toast.sucesso("Movimentação registrada!");
    setQuantidade(0);
    setMotivo("");
    setObservacao("");
    router.refresh();
  }

  const opcoes: [TipoMovimentacao, string, string][] = [
    ["entrada", "Entrada", "bg-emerald-600"],
    ["saida", "Saída", "bg-rose-600"],
    ["ajuste", "Ajuste", "bg-slate-600"],
  ];

  return (
    <div className="card p-6">
      <h2 className="mb-4 font-semibold text-slate-800">Movimentar estoque</h2>
      <form onSubmit={registrar} className="space-y-4">
        <div className="flex gap-2">
          {opcoes.map(([valor, rotulo, cor]) => (
            <button
              key={valor}
              type="button"
              onClick={() => {
                setTipo(valor);
                setMotivo("");
              }}
              className={`flex-1 rounded-lg py-2 text-sm font-medium transition ${
                tipo === valor ? `${cor} text-white` : "bg-slate-100 text-slate-600 hover:bg-slate-200"
              }`}
            >
              {rotulo}
            </button>
          ))}
        </div>

        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label className="label">
              {tipo === "ajuste" ? "Nova quantidade total" : "Quantidade"} ({item.unidade})
            </label>
            <input
              className="input"
              type="number"
              min={0}
              step="any"
              value={quantidade}
              onChange={(e) => setQuantidade(Number(e.target.value))}
              required
            />
            <p className="mt-1 text-xs text-slate-400">
              Atual: {fmtNumero(item.quantidade)} {item.unidade}
            </p>
          </div>
          <div>
            <label className="label">Motivo</label>
            <select className="input" value={motivo} onChange={(e) => setMotivo(e.target.value)}>
              <option value="">Selecione...</option>
              {motivos.map((m) => (
                <option key={m} value={m}>
                  {m}
                </option>
              ))}
            </select>
          </div>
          {tipo === "entrada" && (
            <div>
              <label className="label">Custo unitário (R$)</label>
              <input
                className="input"
                type="number"
                min={0}
                step="0.01"
                value={custoUnitario}
                onChange={(e) => setCustoUnitario(Number(e.target.value))}
              />
              <p className="mt-1 text-xs text-slate-400">Atualiza o custo médio ponderado.</p>
            </div>
          )}
          <div>
            <label className="label">Observação</label>
            <input
              className="input"
              value={observacao}
              onChange={(e) => setObservacao(e.target.value)}
              placeholder="Requisitante, nota fiscal..."
            />
          </div>
        </div>

        <button type="submit" className="btn-primary" disabled={salvando}>
          {salvando ? "Registrando..." : "Registrar movimentação"}
        </button>
      </form>
    </div>
  );
}
