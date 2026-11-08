"use client";

import { useEffect, useState } from "react";
import { createPortal } from "react-dom";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { ROTULO_PAPEL, type PapelUsuario, podeGerenciar } from "@/lib/tipos";
import { MENU } from "@/lib/navegacao";

export default function MenuMobile({
  nome,
  papel,
}: {
  nome: string;
  papel: PapelUsuario;
}) {
  const [aberto, setAberto] = useState(false);
  const [montado, setMontado] = useState(false);
  const pathname = usePathname();
  const gestor = podeGerenciar(papel);

  useEffect(() => setMontado(true), []);

  // Trava o scroll do fundo enquanto o menu está aberto
  useEffect(() => {
    if (aberto) {
      document.body.style.overflow = "hidden";
      return () => {
        document.body.style.overflow = "";
      };
    }
  }, [aberto]);

  const drawer = (
    <div className="fixed inset-0 z-[100] flex md:hidden">
      {/* overlay */}
      <div className="absolute inset-0 bg-slate-900/60" onClick={() => setAberto(false)} />

      {/* painel */}
      <div className="relative flex h-full w-72 max-w-[82vw] flex-col bg-white shadow-2xl dark:bg-neutral-900">
        <div className="flex items-center justify-between border-b border-slate-200 px-4 py-4 dark:border-neutral-800">
          <span className="flex items-center gap-2 font-bold text-slate-800 dark:text-slate-100">
            <span className="text-2xl">📦</span> Almoxarifado
          </span>
          <button
            onClick={() => setAberto(false)}
            className="rounded-lg p-2 text-slate-500 hover:bg-slate-100 dark:hover:bg-neutral-800"
            aria-label="Fechar menu"
          >
            ✕
          </button>
        </div>

        <nav className="flex-1 space-y-1 overflow-y-auto p-3">
          {MENU.map((item) => {
            if (item.gerente && papel !== "gerente") return null;
            if (item.gestor && !gestor) return null;
            const ativo = item.href === "/" ? pathname === "/" : pathname.startsWith(item.href);
            return (
              <Link
                key={item.href}
                href={item.href}
                onClick={() => setAberto(false)}
                className={`flex items-center gap-3 rounded-lg px-3 py-3 text-[15px] font-medium ${
                  ativo
                    ? "bg-brand-50 text-brand-700 dark:bg-neutral-800"
                    : "text-slate-700 hover:bg-slate-100 dark:text-slate-200 dark:hover:bg-neutral-800"
                }`}
              >
                <span className="text-lg">{item.icone}</span>
                {item.rotulo}
              </Link>
            );
          })}
        </nav>

        <Link
          href="/perfil"
          onClick={() => setAberto(false)}
          className="border-t border-slate-200 p-4 hover:bg-slate-50 dark:border-neutral-800 dark:hover:bg-neutral-800"
        >
          <p className="truncate text-sm font-medium text-slate-700 dark:text-slate-200">{nome}</p>
          <span className="badge mt-1 bg-brand-100 text-brand-700">{ROTULO_PAPEL[papel]}</span>
        </Link>
      </div>
    </div>
  );

  return (
    <div className="md:hidden">
      <button
        onClick={() => setAberto(true)}
        className="rounded-lg p-2 text-slate-600 hover:bg-slate-100"
        aria-label="Abrir menu"
      >
        <span className="text-xl">☰</span>
      </button>
      {montado && aberto && createPortal(drawer, document.body)}
    </div>
  );
}
