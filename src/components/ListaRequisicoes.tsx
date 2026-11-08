"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";
import { fmtNumero, fmtDataHora } from "@/lib/format";
import { ROTULO_STATUS_REQ, type Requisicao, type StatusRequisicao } from "@/lib/tipos";

const COR_STATUS: Record<StatusRequisicao, string> = {
  pendente: "bg-amber-100 text-amber-700",
  aprovada: "bg-blue-100 text-blue-700",
  rejeitada: "bg-rose-100 text-rose-700",
  parcial: "bg-cyan-100 text-cyan-700",
  atendida: "bg-emerald-100 text-emerald-700",
  cancelada: "bg-slate-100 text-slate-500",
};

export default function ListaRequisicoes({
  requisicoes,
  podeGerir,
  usuarioId,
}: {
  requisicoes: Requisicao[];
  podeGerir: boolean;
  usuarioId: string;
}) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const toast = useToast();
  const [ocupado, setOcupado] = useState<string | null>(null);
  const [atendendo, setAtendendo] = useState<string | null>(null);
  const [qtds, setQtds] = useState<Record<string, number>>({});

  async function mudar(req: Requisicao, status: StatusRequisicao) {
    setOcupado(req.id);
    const { error } = await supabase
      .from("requisicoes")
      .update({ status, aprovador_id: usuarioId })
      .eq("id", req.id);
    setOcupado(null);
    if (error) return toast.erro(error.message);
    const r: Record<string, string> = {
      aprovada: "Requisição aprovada.",
      rejeitada: "Requisição rejeitada.",
      cancelada: "Requisição cancelada.",
    };
    toast.sucesso(r[status] ?? "Atualizado.");
    router.refresh();
  }

  function abrirAtendimento(req: Requisicao) {
    const inicial: Record<string, number> = {};
    req.requisicao_itens?.forEach((ri) => {
      inicial[ri.id] = ri.quantidade - ri.quantidade_atendida;
    });
    setQtds(inicial);
    setAtendendo(req.id);
  }

  async function confirmarAtendimento(req: Requisicao) {
    setOcupado(req.id);
    const erros: string[] = [];
    let totalFalta = 0;

    for (const ri of req.requisicao_itens ?? []) {
      const atenderAgora = Math.min(qtds[ri.id] ?? 0, ri.quantidade - ri.quantidade_atendida);
      if (atenderAgora > 0) {
        const { error } = await supabase.from("movimentacoes").insert({
          item_id: ri.item_id,
          tipo: "saida",
          quantidade: atenderAgora,
          motivo: "Requisição",
          observacao: `Atendimento de requisição${req.centro_custo ? " · CC " + req.centro_custo : ""}`,
          usuario_id: usuarioId,
        });
        if (error) {
          erros.push(`${ri.itens?.nome ?? "item"}: estoque insuficiente`);
        } else {
          await supabase
            .from("requisicao_itens")
            .update({ quantidade_atendida: ri.quantidade_atendida + atenderAgora })
            .eq("id", ri.id);
        }
      }
      const faltaDepois = ri.quantidade - (ri.quantidade_atendida + (erros.length ? 0 : atenderAgora));
      if (faltaDepois > 0) totalFalta += faltaDepois;
    }

    const novoStatus: StatusRequisicao = totalFalta <= 0 && erros.length === 0 ? "atendida" : "parcial";
    await supabase
      .from("requisicoes")
      .update({ status: novoStatus, aprovador_id: usuarioId })
      .eq("id", req.id);

    setOcupado(null);
    setAtendendo(null);
    if (erros.length > 0) toast.erro("Alguns itens não puderam ser atendidos: " + erros.join("; "));
    else toast.sucesso(novoStatus === "atendida" ? "Requisição atendida." : "Atendimento parcial registrado.");
    router.refresh();
  }

  if (requisicoes.length === 0) {
    return <div className="card p-10 text-center text-slate-400">Nenhuma requisição ainda.</div>;
  }

  return (
    <div className="space-y-3">
      {requisicoes.map((req) => {
        const meu = req.solicitante_id === usuarioId;
        const bloqueado = ocupado === req.id;
        const podeAtender = podeGerir && ["aprovada", "pendente", "parcial"].includes(req.status);
        return (
          <div key={req.id} className="card p-4">
            <div className="flex flex-wrap items-start justify-between gap-3">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span className={`badge ${COR_STATUS[req.status]}`}>{ROTULO_STATUS_REQ[req.status]}</span>
                  <p className="font-semibold text-slate-800">{req.setor ? `Setor: ${req.setor}` : "Requisição"}</p>
                </div>
                <p className="mt-1 text-sm text-slate-500">
                  por {req.solicitante?.nome ?? "—"}
                  {req.centro_custo && <> · CC {req.centro_custo}</>} · {fmtDataHora(req.criado_em)}
                </p>
                {req.justificativa && <p className="mt-1 text-sm text-slate-600">“{req.justificativa}”</p>}
                <ul className="mt-2 space-y-1">
                  {req.requisicao_itens?.map((ri) => {
                    const falta = ri.quantidade - ri.quantidade_atendida;
                    return (
                      <li key={ri.id} className="text-sm text-slate-700">
                        • {ri.itens?.nome ?? "item"} —{" "}
                        <span className="font-medium">
                          {fmtNumero(ri.quantidade)} {ri.itens?.unidade}
                        </span>
                        {ri.quantidade_atendida > 0 && (
                          <span className="text-cyan-700"> (atendido {fmtNumero(ri.quantidade_atendida)})</span>
                        )}
                        {ri.itens && ri.itens.quantidade < falta && (
                          <span className="ml-1 text-xs text-rose-600">
                            (estoque: {fmtNumero(ri.itens.quantidade)})
                          </span>
                        )}
                      </li>
                    );
                  })}
                </ul>
              </div>

              <div className="flex flex-wrap gap-2">
                {podeGerir && req.status === "pendente" && (
                  <>
                    <button className="btn-primary" disabled={bloqueado} onClick={() => mudar(req, "aprovada")}>
                      Aprovar
                    </button>
                    <button className="btn-secondary" disabled={bloqueado} onClick={() => mudar(req, "rejeitada")}>
                      Rejeitar
                    </button>
                  </>
                )}
                {podeAtender && atendendo !== req.id && (
                  <button className="btn-primary" disabled={bloqueado} onClick={() => abrirAtendimento(req)}>
                    Atender
                  </button>
                )}
                {meu && req.status === "pendente" && (
                  <button className="btn-secondary" disabled={bloqueado} onClick={() => mudar(req, "cancelada")}>
                    Cancelar
                  </button>
                )}
              </div>
            </div>

            {atendendo === req.id && (
              <div className="mt-3 space-y-2 rounded-lg bg-slate-50 p-3">
                <p className="text-sm font-medium text-slate-700">Quantidade a atender agora:</p>
                {req.requisicao_itens?.map((ri) => {
                  const falta = ri.quantidade - ri.quantidade_atendida;
                  return (
                    <div key={ri.id} className="flex items-center justify-between gap-2 text-sm">
                      <span className="min-w-0 truncate text-slate-700">
                        {ri.itens?.nome} <span className="text-slate-400">(falta {fmtNumero(falta)})</span>
                      </span>
                      <input
                        className="input w-28"
                        type="number"
                        min={0}
                        max={falta}
                        step="any"
                        value={qtds[ri.id] ?? 0}
                        onChange={(e) => setQtds((q) => ({ ...q, [ri.id]: Number(e.target.value) }))}
                      />
                    </div>
                  );
                })}
                <div className="flex gap-2 pt-1">
                  <button className="btn-primary" disabled={bloqueado} onClick={() => confirmarAtendimento(req)}>
                    Confirmar baixa de estoque
                  </button>
                  <button className="btn-secondary" onClick={() => setAtendendo(null)}>
                    Cancelar
                  </button>
                </div>
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}
