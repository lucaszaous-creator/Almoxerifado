"use client";

import { useEffect, useRef, useState, useCallback } from "react";
import Link from "next/link";
import { criarClienteBrowser } from "@/lib/supabase/client";
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

export default function SinoNotificacoes({ usuarioId }: { usuarioId: string }) {
  const supabase = criarClienteBrowser();
  const [aberto, setAberto] = useState(false);
  const [itens, setItens] = useState<Notificacao[]>([]);
  const ref = useRef<HTMLDivElement>(null);

  const carregar = useCallback(async () => {
    const { data } = await supabase
      .from("notificacoes")
      .select("*")
      .order("criado_em", { ascending: false })
      .limit(30);
    if (data) setItens(data as Notificacao[]);
  }, [supabase]);

  useEffect(() => {
    carregar();
    const canal = supabase
      .channel("notificacoes-realtime")
      .on(
        "postgres_changes",
        {
          event: "*",
          schema: "public",
          table: "notificacoes",
          filter: `usuario_id=eq.${usuarioId}`,
        },
        () => carregar()
      )
      .subscribe();
    return () => {
      supabase.removeChannel(canal);
    };
  }, [supabase, usuarioId, carregar]);

  useEffect(() => {
    function fora(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setAberto(false);
    }
    document.addEventListener("mousedown", fora);
    return () => document.removeEventListener("mousedown", fora);
  }, []);

  const naoLidas = itens.filter((n) => !n.lida).length;

  async function marcarUmaLida(id: string) {
    await supabase.from("notificacoes").update({ lida: true }).eq("id", id);
    setItens((prev) => prev.map((n) => (n.id === id ? { ...n, lida: true } : n)));
  }

  async function marcarTodasLidas() {
    await supabase.from("notificacoes").update({ lida: true }).eq("lida", false);
    setItens((prev) => prev.map((n) => ({ ...n, lida: true })));
  }

  return (
    <div className="relative" ref={ref}>
      <button
        onClick={() => setAberto((v) => !v)}
        className="relative rounded-full p-2 text-slate-600 hover:bg-slate-100"
        aria-label="Notificações"
      >
        <span className="text-xl">🔔</span>
        {naoLidas > 0 && (
          <span className="absolute -right-0.5 -top-0.5 flex h-5 min-w-[1.25rem] items-center justify-center rounded-full bg-rose-500 px-1 text-[11px] font-bold text-white">
            {naoLidas > 99 ? "99+" : naoLidas}
          </span>
        )}
      </button>

      {aberto && (
        <div className="absolute right-0 z-50 mt-2 w-80 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-lg sm:w-96">
          <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
            <p className="font-semibold text-slate-800">Notificações</p>
            {naoLidas > 0 && (
              <button
                onClick={marcarTodasLidas}
                className="text-xs font-medium text-brand-600 hover:underline"
              >
                Marcar todas como lidas
              </button>
            )}
          </div>

          <div className="max-h-96 overflow-y-auto">
            {itens.length === 0 && (
              <p className="px-4 py-8 text-center text-sm text-slate-400">
                Nenhuma notificação.
              </p>
            )}
            {itens.map((n) => (
              <button
                key={n.id}
                onClick={() => marcarUmaLida(n.id)}
                className={`flex w-full gap-3 border-b border-slate-50 px-4 py-3 text-left hover:bg-slate-50 ${
                  n.lida ? "" : "bg-brand-50/40"
                }`}
              >
                <span className="text-lg">{ICONE[n.tipo]}</span>
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-medium text-slate-800">{n.titulo}</p>
                  <p className="text-xs text-slate-500">{n.mensagem}</p>
                  <p className="mt-1 text-[11px] text-slate-400">
                    {new Date(n.criado_em).toLocaleString("pt-BR")}
                  </p>
                </div>
                {!n.lida && <span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-brand-500" />}
              </button>
            ))}
          </div>

          <Link
            href="/notificacoes"
            onClick={() => setAberto(false)}
            className="block border-t border-slate-100 px-4 py-2.5 text-center text-sm font-medium text-brand-600 hover:bg-slate-50"
          >
            Ver todas
          </Link>
        </div>
      )}
    </div>
  );
}
