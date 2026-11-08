"use client";

import { createContext, useCallback, useContext, useRef, useState } from "react";

type OpcoesConfirm = {
  titulo: string;
  mensagem?: string;
  confirmar?: string;
  cancelar?: string;
  perigo?: boolean;
};

type ConfirmCtx = (opcoes: OpcoesConfirm) => Promise<boolean>;

const Ctx = createContext<ConfirmCtx | null>(null);

export function ConfirmProvider({ children }: { children: React.ReactNode }) {
  const [estado, setEstado] = useState<OpcoesConfirm | null>(null);
  const resolver = useRef<((v: boolean) => void) | null>(null);

  const confirm = useCallback<ConfirmCtx>((opcoes) => {
    setEstado(opcoes);
    return new Promise<boolean>((resolve) => {
      resolver.current = resolve;
    });
  }, []);

  function responder(v: boolean) {
    resolver.current?.(v);
    resolver.current = null;
    setEstado(null);
  }

  return (
    <Ctx.Provider value={confirm}>
      {children}
      {estado && (
        <div
          className="fixed inset-0 z-[110] flex items-center justify-center bg-black/40 p-4"
          onClick={() => responder(false)}
        >
          <div
            className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-xl"
            onClick={(e) => e.stopPropagation()}
          >
            <h3 className="text-lg font-semibold text-slate-800">{estado.titulo}</h3>
            {estado.mensagem && <p className="mt-2 text-sm text-slate-600">{estado.mensagem}</p>}
            <div className="mt-6 flex justify-end gap-3">
              <button className="btn-secondary" onClick={() => responder(false)}>
                {estado.cancelar ?? "Cancelar"}
              </button>
              <button
                className={estado.perigo ? "btn-danger" : "btn-primary"}
                onClick={() => responder(true)}
                autoFocus
              >
                {estado.confirmar ?? "Confirmar"}
              </button>
            </div>
          </div>
        </div>
      )}
    </Ctx.Provider>
  );
}

export function useConfirm() {
  const ctx = useContext(Ctx);
  if (!ctx) throw new Error("useConfirm deve ser usado dentro de ConfirmProvider");
  return ctx;
}
