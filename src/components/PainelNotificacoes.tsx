"use client";

import { useState } from "react";
import Link from "next/link";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { fmtDataHora } from "@/lib/format";
import type { Notificacao, TipoNotificacao } from "@/lib/tipos";

const ICONE: Record<TipoNotificacao, string> = {
  estoque_baixo: "⚠️",
  validade_proxima: "⏳",
  validade_vencida: "❌",
  novo_pedido: "🛒",
  pedido_aprovado: "✅",
  pedido_rejeitado: "🚫",
  pedido_recebido: "📥",
  nova_requisicao: "📝",
  requisicao_aprovada: "✅",
  requisicao_rejeitada: "🚫",
  requisicao_atendida: "📦",
};

export default function PainelNotificacoes({ iniciais }: { iniciais: Notificacao[] }) {
  const supabase = criarClienteBrowser();
  const [itens, setItens] = useState(iniciais);

  async function marcarLida(id: string) {
    await supabase.from("notificacoes").update({ lida: true }).eq("id", id);
    setItens((p) => p.map((n) => (n.id === id ? { ...n, lida: true } : n)));
  }

  async function marcarTodas() {
    await supabase.from("notificacoes").update({ lida: true }).eq("lida", false);
    setItens((p) => p.map((n) => ({ ...n, lida: true })));
  }

  const naoLidas = itens.filter((n) => !n.lida).length;

  return (
    <div className="space-y-3">
      {naoLidas > 0 && (
        <div className="flex justify-end">
          <button onClick={marcarTodas} className="btn-secondary">
            Marcar todas como lidas ({naoLidas})
          </button>
        </div>
      )}

      {itens.length === 0 && (
        <div className="card p-10 text-center text-slate-400">Nenhuma notificação.</div>
      )}

      {itens.map((n) => {
        const destino = n.tipo.startsWith("requisicao") || n.tipo === "nova_requisicao"
          ? "/requisicoes"
          : n.item_id
          ? `/itens/${n.item_id}`
          : n.pedido_id
          ? "/compras"
          : null;
        const corpo = (
          <div
            className={`flex gap-3 rounded-xl border p-4 transition ${
              n.lida ? "border-slate-200 bg-white" : "border-brand-200 bg-brand-50/50"
            }`}
          >
            <span className="text-2xl">{ICONE[n.tipo]}</span>
            <div className="min-w-0 flex-1">
              <div className="flex items-center justify-between gap-2">
                <p className="font-semibold text-slate-800">{n.titulo}</p>
                {!n.lida && <span className="h-2 w-2 shrink-0 rounded-full bg-brand-500" />}
              </div>
              <p className="text-sm text-slate-600">{n.mensagem}</p>
              <p className="mt-1 text-xs text-slate-400">{fmtDataHora(n.criado_em)}</p>
            </div>
          </div>
        );

        return (
          <div key={n.id} onClick={() => !n.lida && marcarLida(n.id)}>
            {destino ? <Link href={destino}>{corpo}</Link> : corpo}
          </div>
        );
      })}
    </div>
  );
}
