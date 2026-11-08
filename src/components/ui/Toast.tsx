"use client";

import { createContext, useCallback, useContext, useState } from "react";

type TipoToast = "sucesso" | "erro" | "info";
type Toast = { id: number; tipo: TipoToast; mensagem: string };

type ToastCtx = {
  toast: (mensagem: string, tipo?: TipoToast) => void;
  sucesso: (m: string) => void;
  erro: (m: string) => void;
};

const Ctx = createContext<ToastCtx | null>(null);

const ESTILO: Record<TipoToast, { icone: string; classe: string }> = {
  sucesso: { icone: "✅", classe: "border-emerald-200 bg-white" },
  erro: { icone: "⛔", classe: "border-rose-200 bg-white" },
  info: { icone: "ℹ️", classe: "border-slate-200 bg-white" },
};

export function ToastProvider({ children }: { children: React.ReactNode }) {
  const [toasts, setToasts] = useState<Toast[]>([]);

  const remover = useCallback((id: number) => {
    setToasts((ts) => ts.filter((t) => t.id !== id));
  }, []);

  const toast = useCallback(
    (mensagem: string, tipo: TipoToast = "info") => {
      const id = Date.now() + Math.random();
      setToasts((ts) => [...ts, { id, tipo, mensagem }]);
      setTimeout(() => remover(id), 4000);
    },
    [remover]
  );

  const valor: ToastCtx = {
    toast,
    sucesso: (m) => toast(m, "sucesso"),
    erro: (m) => toast(m, "erro"),
  };

  return (
    <Ctx.Provider value={valor}>
      {children}
      <div className="pointer-events-none fixed right-4 top-4 z-[100] flex w-80 max-w-[calc(100vw-2rem)] flex-col gap-2">
        {toasts.map((t) => (
          <div
            key={t.id}
            role="status"
            className={`pointer-events-auto flex items-start gap-3 rounded-xl border px-4 py-3 shadow-lg ${ESTILO[t.tipo].classe} animate-[slideIn_.2s_ease-out]`}
          >
            <span className="text-lg leading-none">{ESTILO[t.tipo].icone}</span>
            <p className="flex-1 text-sm text-slate-700">{t.mensagem}</p>
            <button
              onClick={() => remover(t.id)}
              className="text-slate-300 hover:text-slate-500"
              aria-label="Fechar"
            >
              ✕
            </button>
          </div>
        ))}
      </div>
    </Ctx.Provider>
  );
}

export function useToast() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useToast deve ser usado dentro de ToastProvider");
  return ctx;
}
