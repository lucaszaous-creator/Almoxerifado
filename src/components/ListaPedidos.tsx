"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";
import { fmtNumero, fmtMoeda, fmtDataHora } from "@/lib/format";
import { ROTULO_STATUS, type PedidoCompra, type StatusPedido } from "@/lib/tipos";

const COR_STATUS: Record<StatusPedido, string> = {
  pendente: "bg-amber-100 text-amber-700",
  aprovado: "bg-blue-100 text-blue-700",
  rejeitado: "bg-rose-100 text-rose-700",
  comprado: "bg-indigo-100 text-indigo-700",
  parcial: "bg-cyan-100 text-cyan-700",
  recebido: "bg-emerald-100 text-emerald-700",
  cancelado: "bg-slate-100 text-slate-500",
};

export default function ListaPedidos({
  pedidos,
  podeGerir,
  usuarioId,
}: {
  pedidos: PedidoCompra[];
  podeGerir: boolean;
  usuarioId: string;
}) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const toast = useToast();
  const [ocupado, setOcupado] = useState<string | null>(null);
  const [recebendo, setRecebendo] = useState<string | null>(null);
  const [qtdReceber, setQtdReceber] = useState<number>(0);

  async function mudarStatus(p: PedidoCompra, status: StatusPedido) {
    setOcupado(p.id);
    const patch: Record<string, unknown> = { status };
    if (status === "aprovado" || status === "rejeitado") patch.aprovador_id = usuarioId;
    const { error } = await supabase.from("pedidos_compra").update(patch).eq("id", p.id);
    setOcupado(null);
    if (error) return toast.erro(error.message);
    toast.sucesso("Pedido atualizado.");
    router.refresh();
  }

  async function receber(p: PedidoCompra) {
    const restante = p.quantidade_solicitada - p.quantidade_recebida;
    const receberQtd = Math.min(qtdReceber, restante);
    if (receberQtd <= 0) return toast.erro("Informe uma quantidade válida.");

    setOcupado(p.id);
    if (p.item_id) {
      const { error: e1 } = await supabase.from("movimentacoes").insert({
        item_id: p.item_id,
        tipo: "entrada",
        quantidade: receberQtd,
        motivo: "Compra",
        custo_unitario: p.preco_estimado || null,
        observacao: `Recebimento do pedido #${p.numero ?? ""}`,
        usuario_id: usuarioId,
      });
      if (e1) {
        setOcupado(null);
        return toast.erro(e1.message);
      }
    }
    const novaRecebida = p.quantidade_recebida + receberQtd;
    const novoStatus: StatusPedido = novaRecebida >= p.quantidade_solicitada ? "recebido" : "parcial";
    await supabase
      .from("pedidos_compra")
      .update({ quantidade_recebida: novaRecebida, status: novoStatus })
      .eq("id", p.id);
    setOcupado(null);
    setRecebendo(null);
    toast.sucesso(novoStatus === "recebido" ? "Pedido recebido por completo." : "Recebimento parcial registrado.");
    router.refresh();
  }

  if (pedidos.length === 0) {
    return <div className="card p-10 text-center text-slate-400">Nenhum pedido de compra ainda.</div>;
  }

  return (
    <div className="space-y-3">
      {pedidos.map((p) => {
        const meu = p.solicitante_id === usuarioId;
        const bloqueado = ocupado === p.id;
        const restante = p.quantidade_solicitada - p.quantidade_recebida;
        const valor = p.quantidade_solicitada * (p.preco_estimado || 0);
        const podeReceber = podeGerir && ["aprovado", "comprado", "parcial"].includes(p.status);
        return (
          <div key={p.id} className="card p-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="flex flex-wrap items-center gap-2">
                  {p.numero != null && (
                    <span className="badge bg-slate-100 text-slate-500">#{p.numero}</span>
                  )}
                  <span className={`badge ${COR_STATUS[p.status]}`}>{ROTULO_STATUS[p.status]}</span>
                  <p className="font-semibold text-slate-800">{p.descricao_item}</p>
                </div>
                <p className="mt-1 text-sm text-slate-500">
                  {fmtNumero(p.quantidade_solicitada)} {p.unidade}
                  {p.preco_estimado > 0 && <> · est. {fmtMoeda(valor)}</>}
                  {p.fornecedores?.nome && <> · 🏭 {p.fornecedores.nome}</>}
                  {" · por "}
                  {p.solicitante?.nome ?? "—"} · {fmtDataHora(p.criado_em)}
                </p>
                {p.quantidade_recebida > 0 && (
                  <p className="text-xs text-cyan-700">
                    Recebido: {fmtNumero(p.quantidade_recebida)} de {fmtNumero(p.quantidade_solicitada)} {p.unidade}
                  </p>
                )}
                {p.justificativa && <p className="mt-1 text-sm text-slate-600">“{p.justificativa}”</p>}
                {p.anexo_url && (
                  <a
                    href={p.anexo_url}
                    target="_blank"
                    rel="noreferrer"
                    className="mt-1 inline-block text-sm text-brand-600 hover:underline"
                  >
                    📎 Ver anexo
                  </a>
                )}
              </div>

              <div className="flex flex-wrap gap-2">
                {podeGerir && p.status === "pendente" && (
                  <>
                    <button className="btn-primary" disabled={bloqueado} onClick={() => mudarStatus(p, "aprovado")}>
                      Aprovar
                    </button>
                    <button className="btn-secondary" disabled={bloqueado} onClick={() => mudarStatus(p, "rejeitado")}>
                      Rejeitar
                    </button>
                  </>
                )}
                {podeGerir && p.status === "aprovado" && (
                  <button className="btn-secondary" disabled={bloqueado} onClick={() => mudarStatus(p, "comprado")}>
                    Marcar comprado
                  </button>
                )}
                {podeReceber && recebendo !== p.id && (
                  <button
                    className="btn-primary"
                    disabled={bloqueado}
                    onClick={() => {
                      setRecebendo(p.id);
                      setQtdReceber(restante);
                    }}
                  >
                    Receber
                  </button>
                )}
                {meu && p.status === "pendente" && (
                  <button
                    className="btn-secondary"
                    disabled={bloqueado}
                    onClick={() => mudarStatus(p, "cancelado")}
                  >
                    Cancelar
                  </button>
                )}
              </div>
            </div>

            {recebendo === p.id && (
              <div className="mt-3 flex flex-wrap items-end gap-3 rounded-lg bg-slate-50 p-3">
                <div>
                  <label className="label">Quantidade recebida agora</label>
                  <input
                    className="input w-40"
                    type="number"
                    min={0}
                    max={restante}
                    step="any"
                    value={qtdReceber}
                    onChange={(e) => setQtdReceber(Number(e.target.value))}
                  />
                  <p className="mt-1 text-xs text-slate-400">Restante: {fmtNumero(restante)} {p.unidade}</p>
                </div>
                <button className="btn-primary" disabled={bloqueado} onClick={() => receber(p)}>
                  Confirmar entrada
                </button>
                <button className="btn-secondary" onClick={() => setRecebendo(null)}>
                  Cancelar
                </button>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
