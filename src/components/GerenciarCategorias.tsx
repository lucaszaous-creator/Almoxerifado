"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import type { Categoria } from "@/lib/tipos";

export default function GerenciarCategorias({ categorias }: { categorias: Categoria[] }) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const [aberto, setAberto] = useState(false);
  const [nome, setNome] = useState("");
  const [erro, setErro] = useState<string | null>(null);

  async function adicionar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    const n = nome.trim();
    if (!n) return;
    const { error } = await supabase.from("categorias").insert({ nome: n });
    if (error) {
      setErro(error.message.includes("duplicate") ? "Categoria já existe." : error.message);
      return;
    }
    setNome("");
    router.refresh();
  }

  return (
    <div className="card p-4">
      <button
        onClick={() => setAberto((v) => !v)}
        className="flex w-full items-center justify-between text-sm font-medium text-slate-700"
      >
        <span>Categorias ({categorias.length})</span>
        <span className="text-slate-400">{aberto ? "▲" : "▼"}</span>
      </button>

      {aberto && (
        <div className="mt-3 space-y-3">
          <div className="flex flex-wrap gap-2">
            {categorias.map((c) => (
              <span key={c.id} className="badge bg-slate-100 text-slate-600">
                {c.nome}
              </span>
            ))}
            {categorias.length === 0 && (
              <span className="text-sm text-slate-400">Nenhuma categoria ainda.</span>
            )}
          </div>
          <form onSubmit={adicionar} className="flex gap-2">
            <input
              className="input"
              placeholder="Nova categoria"
              value={nome}
              onChange={(e) => setNome(e.target.value)}
            />
            <button type="submit" className="btn-secondary whitespace-nowrap">
              Adicionar
            </button>
          </form>
          {erro && <p className="text-sm text-rose-600">{erro}</p>}
        </div>
      )}
    </div>
  );
}
