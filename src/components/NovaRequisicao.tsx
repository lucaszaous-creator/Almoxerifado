"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { fmtNumero } from "@/lib/format";
import type { Item } from "@/lib/tipos";

type LinhaItem = { item_id: string; quantidade: number };

export default function NovaRequisicao({
  itens,
  usuarioId,
}: {
  itens: Item[];
  usuarioId: string;
}) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const [aberto, setAberto] = useState(false);
  const [setor, setSetor] = useState("");
  const [centroCusto, setCentroCusto] = useState("");
  const [justificativa, setJustificativa] = useState("");
  const [linhas, setLinhas] = useState<LinhaItem[]>([{ item_id: "", quantidade: 1 }]);
  const [erro, setErro] = useState<string | null>(null);
  const [salvando, setSalvando] = useState(false);

  function atualizarLinha(i: number, campo: keyof LinhaItem, valor: string | number) {
    setLinhas((ls) => ls.map((l, idx) => (idx === i ? { ...l, [campo]: valor } : l)));
  }
  function adicionarLinha() {
    setLinhas((ls) => [...ls, { item_id: "", quantidade: 1 }]);
  }
  function removerLinha(i: number) {
    setLinhas((ls) => (ls.length === 1 ? ls : ls.filter((_, idx) => idx !== i)));
  }

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    const validas = linhas.filter((l) => l.item_id && l.quantidade > 0);
    if (validas.length === 0) {
      setErro("Adicione ao menos um item com quantidade.");
      return;
    }
    setSalvando(true);
    const { data: req, error } = await supabase
      .from("requisicoes")
      .insert({
        solicitante_id: usuarioId,
        setor: setor.trim() || null,
        centro_custo: centroCusto.trim() || null,
        justificativa: justificativa.trim() || null,
      })
      .select()
      .single();

    if (error || !req) {
      setErro(error?.message ?? "Erro ao criar requisição.");
      setSalvando(false);
      return;
    }

    const { error: errItens } = await supabase.from("requisicao_itens").insert(
      validas.map((l) => ({
        requisicao_id: req.id,
        item_id: l.item_id,
        quantidade: l.quantidade,
      }))
    );
    setSalvando(false);
    if (errItens) {
      setErro(errItens.message);
      return;
    }

    setSetor("");
    setCentroCusto("");
    setJustificativa("");
    setLinhas([{ item_id: "", quantidade: 1 }]);
    setAberto(false);
    router.refresh();
  }

  if (!aberto) {
    return (
      <button onClick={() => setAberto(true)} className="btn-primary">
        + Nova requisição
      </button>
    );
  }

  return (
    <form onSubmit={enviar} className="card space-y-4 p-6">
      <h2 className="font-semibold text-slate-800">Nova requisição de material</h2>

      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="label">Setor / finalidade</label>
          <input
            className="input"
            value={setor}
            onChange={(e) => setSetor(e.target.value)}
            placeholder="Ex.: Manutenção, Recepção..."
          />
        </div>
        <div>
          <label className="label">Centro de custo</label>
          <input
            className="input"
            value={centroCusto}
            onChange={(e) => setCentroCusto(e.target.value)}
            placeholder="Ex.: CC-101, Obra Norte..."
          />
        </div>
        <div className="sm:col-span-2">
          <label className="label">Justificativa</label>
          <input
            className="input"
            value={justificativa}
            onChange={(e) => setJustificativa(e.target.value)}
            placeholder="Para que será usado?"
          />
        </div>
      </div>

      <div className="space-y-2">
        <label className="label">Itens solicitados</label>
        {linhas.map((l, i) => {
          const it = itens.find((x) => x.id === l.item_id);
          return (
            <div key={i} className="flex items-center gap-2">
              <select
                className="input flex-1"
                value={l.item_id}
                onChange={(e) => atualizarLinha(i, "item_id", e.target.value)}
              >
                <option value="">Selecione um item...</option>
                {itens.map((x) => (
                  <option key={x.id} value={x.id}>
                    {x.nome} ({fmtNumero(x.quantidade)} {x.unidade})
                  </option>
                ))}
              </select>
              <input
                className="input w-28"
                type="number"
                min={0}
                step="any"
                value={l.quantidade}
                onChange={(e) => atualizarLinha(i, "quantidade", Number(e.target.value))}
              />
              <span className="w-8 text-sm text-slate-400">{it?.unidade ?? ""}</span>
              <button
                type="button"
                onClick={() => removerLinha(i)}
                className="rounded-lg p-2 text-slate-400 hover:bg-slate-100"
                aria-label="Remover"
              >
                ✕
              </button>
            </div>
          );
        })}
        <button type="button" onClick={adicionarLinha} className="text-sm font-medium text-brand-600 hover:underline">
          + Adicionar item
        </button>
      </div>

      {erro && <p className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">{erro}</p>}

      <div className="flex gap-3">
        <button type="submit" className="btn-primary" disabled={salvando}>
          {salvando ? "Enviando..." : "Enviar requisição"}
        </button>
        <button type="button" className="btn-secondary" onClick={() => setAberto(false)}>
          Cancelar
        </button>
      </div>
    </form>
  );
}
