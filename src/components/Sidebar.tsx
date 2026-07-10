"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { ROTULO_PAPEL, type PapelUsuario, podeGerenciar } from "@/lib/tipos";
import { MENU, GRUPOS } from "@/lib/navegacao";

export default function Sidebar({
  nome,
  papel,
}: {
  nome: string;
  papel: PapelUsuario;
}) {
  const pathname = usePathname();
  const gestor = podeGerenciar(papel);

  function visivel(item: (typeof MENU)[number]) {
    if (item.gerente && papel !== "gerente") return false;
    if (item.gestor && !gestor) return false;
    return true;
  }

  return (
    <aside className="hidden w-64 shrink-0 flex-col border-r border-slate-200 bg-white md:flex">
      <div className="flex items-center gap-2.5 border-b border-slate-200 px-5 py-4">
        <span className="flex h-9 w-9 items-center justify-center rounded-xl bg-brand-600 text-lg text-white shadow-sm">
          📦
        </span>
        <div>
          <p className="text-sm font-bold leading-tight text-slate-800">Almoxarifado</p>
          <p className="text-xs text-slate-400">&amp; Compras</p>
        </div>
      </div>

      <nav className="flex-1 space-y-4 overflow-y-auto p-3">
        {GRUPOS.map((grupo) => {
          const itens = MENU.filter((i) => i.grupo === grupo && visivel(i));
          if (itens.length === 0) return null;
          return (
            <div key={grupo}>
              <p className="px-3 pb-1 text-[11px] font-semibold uppercase tracking-wider text-slate-400">
                {grupo}
              </p>
              <div className="space-y-0.5">
                {itens.map((item) => {
                  const ativo =
                    item.href === "/" ? pathname === "/" : pathname.startsWith(item.href);
                  return (
                    <Link
                      key={item.href}
                      href={item.href}
                      className={`relative flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition ${
                        ativo
                          ? "bg-brand-50 text-brand-700"
                          : "text-slate-600 hover:bg-slate-100 hover:text-slate-900"
                      }`}
                    >
                      {ativo && (
                        <span className="absolute left-0 top-1/2 h-5 w-1 -translate-y-1/2 rounded-r-full bg-brand-600" />
                      )}
                      <span className="text-base">{item.icone}</span>
                      {item.rotulo}
                    </Link>
                  );
                })}
              </div>
            </div>
          );
        })}
      </nav>

      <Link
        href="/perfil"
        className="flex items-center gap-3 border-t border-slate-200 p-4 hover:bg-slate-50"
      >
        <span className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-100 font-semibold text-brand-700">
          {nome.charAt(0).toUpperCase()}
        </span>
        <div className="min-w-0">
          <p className="truncate text-sm font-medium text-slate-700">{nome}</p>
          <p className="text-xs text-slate-400">{ROTULO_PAPEL[papel]}</p>
        </div>
      </Link>
    </aside>
  );
}
