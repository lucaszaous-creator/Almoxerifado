"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { fmtNumero } from "@/lib/format";
import { sanitizarBusca } from "@/lib/db";
import type { Item } from "@/lib/tipos";

export default function BuscaGlobal() {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const [termo, setTermo] = useState("");
  const [resultados, setResultados] = useState<Item[]>([]);
  const [aberto, setAberto] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  // Atalho "/" foca a busca (quando não se está digitando em outro campo)
  useEffect(() => {
    function atalho(e: KeyboardEvent) {
      if (e.key !== "/") return;
      const alvo = e.target as HTMLElement;
      const digitando = ["INPUT", "TEXTAREA", "SELECT"].includes(alvo.tagName) || alvo.isContentEditable;
      if (!digitando) {
        e.preventDefault();
        inputRef.current?.focus();
      }
    }
    document.addEventListener("keydown", atalho);
    return () => document.removeEventListener("keydown", atalho);
  }, []);

  useEffect(() => {
    const t = sanitizarBusca(termo);
    if (t.length < 2) {
      setResultados([]);
      return;
    }
    const timer = setTimeout(async () => {
      const { data } = await supabase
        .from("itens")
        .select("id, nome, codigo, unidade, quantidade, localizacao")
        .or(`nome.ilike.%${t}%,codigo.ilike.%${t}%,codigo_barras.ilike.%${t}%,localizacao.ilike.%${t}%`)
        .order("nome")
        .limit(8);
      setResultados((data ?? []) as Item[]);
      setAberto(true);
    }, 250);
    return () => clearTimeout(timer);
  }, [termo, supabase]);

  useEffect(() => {
    function fora(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setAberto(false);
    }
    document.addEventListener("mousedown", fora);
    return () => document.removeEventListener("mousedown", fora);
  }, []);

  function irPara(id: string) {
    setAberto(false);
    setTermo("");
    router.push(`/itens/${id}`);
  }

  return (
    <div ref={ref} className="relative w-full max-w-md">
      <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">
        🔍
      </span>
      <input
        ref={inputRef}
        className="input h-9 pl-9 pr-8"
        placeholder="Buscar item...  ( / )"
        value={termo}
        onChange={(e) => setTermo(e.target.value)}
        onFocus={() => resultados.length > 0 && setAberto(true)}
        onKeyDown={(e) => e.key === "Escape" && (setAberto(false), inputRef.current?.blur())}
      />
      {aberto && termo.trim().length >= 2 && (
        <div className="absolute left-0 right-0 z-50 mt-1 overflow-hidden rounded-xl border border-slate-200 bg-white shadow-lg">
          {resultados.length === 0 ? (
            <p className="px-4 py-3 text-sm text-slate-400">Nenhum item encontrado.</p>
          ) : (
            resultados.map((i) => (
              <button
                key={i.id}
                onClick={() => irPara(i.id)}
                className="flex w-full items-center justify-between gap-2 border-b border-slate-50 px-4 py-2 text-left hover:bg-slate-50"
              >
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-slate-800">{i.nome}</p>
                  <p className="text-xs text-slate-400">
                    cód. {i.codigo}
                    {i.localizacao ? ` · ${i.localizacao}` : ""}
                  </p>
                </div>
                <span className="whitespace-nowrap text-xs font-medium text-slate-500">
                  {fmtNumero(i.quantidade)} {i.unidade}
                </span>
              </button>
            ))
          )}
        </div>
      )}
    </div>
  );
}
