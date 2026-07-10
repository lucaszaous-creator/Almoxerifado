"use client";

import { useMemo, useState } from "react";
import Link from "next/link";
import { fmtNumero, fmtMoeda, fmtData, diasAteValidade } from "@/lib/format";
import { precisaRepor, type Item, type Categoria, type Fornecedor } from "@/lib/tipos";

type Filtro = "todos" | "baixo" | "reposicao" | "validade" | "inativos";
type Ordem = "nome" | "menor_qtd" | "maior_qtd" | "maior_valor";

export default function ItensLista({
  itens,
  categorias,
  fornecedores,
  filtroInicial,
  podeGerenciar,
}: {
  itens: Item[];
  categorias: Categoria[];
  fornecedores: Fornecedor[];
  filtroInicial: string;
  podeGerenciar: boolean;
}) {
  const [busca, setBusca] = useState("");
  const [categoria, setCategoria] = useState("");
  const [fornecedor, setFornecedor] = useState("");
  const [ordem, setOrdem] = useState<Ordem>("nome");
  const [filtro, setFiltro] = useState<Filtro>(
    filtroInicial === "baixo" || filtroInicial === "validade" ? (filtroInicial as Filtro) : "todos"
  );

  const filtrados = useMemo(() => {
    const q = busca.trim().toLowerCase();
    let lista = itens.filter((i) => {
      if (filtro !== "inativos" && !i.ativo) return false;
      if (filtro === "inativos" && i.ativo) return false;
      if (q) {
        const alvo = `${i.nome} ${i.codigo} ${i.codigo_barras ?? ""} ${i.descricao ?? ""} ${
          i.localizacao ?? ""
        } ${i.categorias?.nome ?? ""} ${i.marca ?? ""}`.toLowerCase();
        if (!alvo.includes(q)) return false;
      }
      if (categoria && i.categoria_id !== categoria) return false;
      if (fornecedor && i.fornecedor_id !== fornecedor) return false;
      if (filtro === "baixo" && !(i.estoque_minimo > 0 && i.quantidade <= i.estoque_minimo)) return false;
      if (filtro === "reposicao" && !precisaRepor(i)) return false;
      if (filtro === "validade") {
        const d = diasAteValidade(i.data_validade);
        if (d === null || d > 30) return false;
      }
      return true;
    });

    lista = [...lista].sort((a, b) => {
      if (ordem === "menor_qtd") return a.quantidade - b.quantidade;
      if (ordem === "maior_qtd") return b.quantidade - a.quantidade;
      if (ordem === "maior_valor")
        return b.quantidade * (b.custo_medio || b.preco_custo) - a.quantidade * (a.custo_medio || a.preco_custo);
      return a.nome.localeCompare(b.nome);
    });
    return lista;
  }, [itens, busca, categoria, fornecedor, filtro, ordem]);

  const chips: [Filtro, string][] = [
    ["todos", "Ativos"],
    ["baixo", "Estoque baixo"],
    ["reposicao", "Repor"],
    ["validade", "Validade ≤ 30d"],
    ["inativos", "Inativos"],
  ];

  return (
    <div className="space-y-4">
      <div className="card space-y-3 p-4">
        <div className="flex flex-col gap-3 lg:flex-row">
          <div className="relative flex-1">
            <span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-slate-400">🔍</span>
            <input
              className="input pl-9"
              placeholder="Buscar por nome, código, código de barras, marca..."
              value={busca}
              onChange={(e) => setBusca(e.target.value)}
              autoFocus
            />
          </div>
          <select className="input lg:w-44" value={categoria} onChange={(e) => setCategoria(e.target.value)}>
            <option value="">Todas categorias</option>
            {categorias.map((c) => (
              <option key={c.id} value={c.id}>
                {c.nome}
              </option>
            ))}
          </select>
          <select className="input lg:w-44" value={fornecedor} onChange={(e) => setFornecedor(e.target.value)}>
            <option value="">Todos fornecedores</option>
            {fornecedores.map((f) => (
              <option key={f.id} value={f.id}>
                {f.nome}
              </option>
            ))}
          </select>
          <select className="input lg:w-40" value={ordem} onChange={(e) => setOrdem(e.target.value as Ordem)}>
            <option value="nome">Ordenar: nome</option>
            <option value="menor_qtd">Menor estoque</option>
            <option value="maior_qtd">Maior estoque</option>
            <option value="maior_valor">Maior valor</option>
          </select>
        </div>

        <div className="flex flex-wrap gap-2">
          {chips.map(([valor, rotulo]) => (
            <button
              key={valor}
              onClick={() => setFiltro(valor)}
              className={`rounded-full px-3 py-1 text-sm font-medium transition ${
                filtro === valor ? "bg-brand-600 text-white" : "bg-slate-100 text-slate-600 hover:bg-slate-200"
              }`}
            >
              {rotulo}
            </button>
          ))}
        </div>
      </div>

      <p className="text-sm text-slate-500">
        {filtrados.length} {filtrados.length === 1 ? "item encontrado" : "itens encontrados"}
      </p>

      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="th">Item</th>
                <th className="th">Local</th>
                <th className="th">Categoria</th>
                <th className="th text-right">Qtd.</th>
                <th className="th text-right">Valor</th>
                <th className="th">Situação</th>
              </tr>
            </thead>
            <tbody>
              {filtrados.map((i) => {
                const baixo = i.estoque_minimo > 0 && i.quantidade <= i.estoque_minimo;
                const d = diasAteValidade(i.data_validade);
                return (
                  <tr key={i.id} className="border-t border-slate-100 hover:bg-slate-50">
                    <td className="px-4 py-3">
                      <div className="flex items-center gap-3">
                        {i.imagem_url ? (
                          // eslint-disable-next-line @next/next/no-img-element
                          <img src={i.imagem_url} alt="" className="h-9 w-9 shrink-0 rounded-lg border border-slate-200 object-cover" />
                        ) : (
                          <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-lg bg-slate-100 text-slate-300">
                            📦
                          </span>
                        )}
                        <div className="min-w-0">
                          <Link href={`/itens/${i.id}`} className="font-medium text-slate-800 hover:text-brand-600">
                            {i.nome}
                          </Link>
                          <p className="text-xs text-slate-400">cód. {i.codigo}</p>
                        </div>
                      </div>
                    </td>
                    <td className="px-4 py-3 text-slate-600">{i.localizacao || "—"}</td>
                    <td className="px-4 py-3 text-slate-600">{i.categorias?.nome || "—"}</td>
                    <td className="px-4 py-3 text-right font-medium text-slate-800">
                      {fmtNumero(i.quantidade)} {i.unidade}
                    </td>
                    <td className="px-4 py-3 text-right text-slate-600">
                      {fmtMoeda(i.quantidade * (i.custo_medio || i.preco_custo))}
                    </td>
                    <td className="px-4 py-3">
                      <div className="flex flex-wrap gap-1">
                        {!i.ativo && <span className="badge bg-slate-200 text-slate-500">inativo</span>}
                        {baixo && <span className="badge bg-amber-100 text-amber-700">estoque baixo</span>}
                        {d !== null && d < 0 && <span className="badge bg-rose-100 text-rose-700">vencido</span>}
                        {d !== null && d >= 0 && d <= 30 && (
                          <span className="badge bg-orange-100 text-orange-700">vence {fmtData(i.data_validade)}</span>
                        )}
                        {i.ativo && !baixo && (d === null || d > 30) && (
                          <span className="badge bg-emerald-100 text-emerald-700">ok</span>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
              {filtrados.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-10 text-center text-slate-400">
                    Nenhum item encontrado.
                    {podeGerenciar && (
                      <>
                        {" "}
                        <Link href="/itens/novo" className="text-brand-600 hover:underline">
                          Cadastrar novo item
                        </Link>
                        .
                      </>
                    )}
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
