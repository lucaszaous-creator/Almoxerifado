"use client";

import { useState } from "react";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { baixarCsv } from "@/lib/csv";
import { fmtData, fmtDataHora } from "@/lib/format";
import {
  ROTULO_STATUS,
  ROTULO_STATUS_REQ,
  type Item,
  type Movimentacao,
  type PedidoCompra,
  type Requisicao,
} from "@/lib/tipos";

const ROTULO_MOV: Record<string, string> = { entrada: "Entrada", saida: "Saída", ajuste: "Ajuste" };

export default function ExportarRelatorios() {
  const supabase = criarClienteBrowser();
  const [carregando, setCarregando] = useState<string | null>(null);

  async function exportarEstoque() {
    setCarregando("estoque");
    const { data } = await supabase
      .from("itens")
      .select("*, categorias(nome), fornecedores(nome)")
      .order("nome");
    const itens = (data ?? []) as Item[];
    baixarCsv(
      `estoque-${hoje()}.csv`,
      [
        "Código", "Item", "Categoria", "Fornecedor", "Localização", "Quantidade", "Unidade",
        "Estoque mínimo", "Custo unitário", "Valor total", "Validade", "Situação",
      ],
      itens.map((i) => [
        i.codigo,
        i.nome,
        i.categorias?.nome ?? "",
        i.fornecedores?.nome ?? "",
        i.localizacao ?? "",
        i.quantidade,
        i.unidade,
        i.estoque_minimo,
        (i.preco_custo ?? 0).toFixed(2).replace(".", ","),
        (i.quantidade * (i.preco_custo ?? 0)).toFixed(2).replace(".", ","),
        i.data_validade ? fmtData(i.data_validade) : "",
        i.estoque_minimo > 0 && i.quantidade <= i.estoque_minimo ? "Estoque baixo" : "OK",
      ])
    );
    setCarregando(null);
  }

  async function exportarMovimentacoes() {
    setCarregando("movimentacoes");
    const { data } = await supabase
      .from("movimentacoes")
      .select("*, itens(nome, codigo, unidade), profiles(nome)")
      .order("criado_em", { ascending: false })
      .limit(5000);
    const movs = (data ?? []) as Movimentacao[];
    baixarCsv(
      `movimentacoes-${hoje()}.csv`,
      ["Data", "Item", "Código", "Tipo", "Quantidade", "Unidade", "Responsável", "Observação"],
      movs.map((m) => [
        fmtDataHora(m.criado_em),
        m.itens?.nome ?? "",
        m.itens?.codigo ?? "",
        ROTULO_MOV[m.tipo] ?? m.tipo,
        m.quantidade,
        m.itens?.unidade ?? "",
        m.profiles?.nome ?? "",
        m.observacao ?? "",
      ])
    );
    setCarregando(null);
  }

  async function exportarCompras() {
    setCarregando("compras");
    const { data } = await supabase
      .from("pedidos_compra")
      .select("*, solicitante:profiles!pedidos_compra_solicitante_id_fkey(nome)")
      .order("criado_em", { ascending: false });
    const pedidos = (data ?? []) as PedidoCompra[];
    baixarCsv(
      `compras-${hoje()}.csv`,
      ["Data", "Item", "Quantidade", "Unidade", "Status", "Solicitante", "Justificativa"],
      pedidos.map((p) => [
        fmtDataHora(p.criado_em),
        p.descricao_item,
        p.quantidade_solicitada,
        p.unidade ?? "",
        ROTULO_STATUS[p.status],
        p.solicitante?.nome ?? "",
        p.justificativa ?? "",
      ])
    );
    setCarregando(null);
  }

  async function exportarRequisicoes() {
    setCarregando("requisicoes");
    const { data } = await supabase
      .from("requisicoes")
      .select(
        "*, solicitante:profiles!requisicoes_solicitante_id_fkey(nome), requisicao_itens(quantidade, itens(nome, unidade))"
      )
      .order("criado_em", { ascending: false });
    const reqs = (data ?? []) as Requisicao[];
    const linhas: unknown[][] = [];
    for (const r of reqs) {
      for (const ri of r.requisicao_itens ?? []) {
        linhas.push([
          fmtDataHora(r.criado_em),
          r.setor ?? "",
          ROTULO_STATUS_REQ[r.status],
          r.solicitante?.nome ?? "",
          ri.itens?.nome ?? "",
          ri.quantidade,
          ri.itens?.unidade ?? "",
        ]);
      }
    }
    baixarCsv(
      `requisicoes-${hoje()}.csv`,
      ["Data", "Setor", "Status", "Solicitante", "Item", "Quantidade", "Unidade"],
      linhas
    );
    setCarregando(null);
  }

  const botoes: [string, string, string, () => Promise<void>][] = [
    ["estoque", "📦 Estoque atual", "Posição de todos os itens", exportarEstoque],
    ["movimentacoes", "🔄 Movimentações", "Entradas, saídas e ajustes", exportarMovimentacoes],
    ["compras", "🛒 Compras", "Pedidos de compra", exportarCompras],
    ["requisicoes", "📝 Requisições", "Requisições de material", exportarRequisicoes],
  ];

  return (
    <div className="card p-4">
      <h2 className="mb-3 font-semibold text-slate-800">Exportar CSV</h2>
      <div className="grid gap-3 sm:grid-cols-2">
        {botoes.map(([chave, titulo, desc, acao]) => (
          <button
            key={chave}
            onClick={acao}
            disabled={carregando !== null}
            className="flex items-center justify-between rounded-lg border border-slate-200 px-4 py-3 text-left transition hover:border-brand-300 hover:bg-brand-50/40 disabled:opacity-60"
          >
            <div>
              <p className="text-sm font-medium text-slate-800">{titulo}</p>
              <p className="text-xs text-slate-400">{desc}</p>
            </div>
            <span className="text-slate-400">{carregando === chave ? "..." : "⬇️"}</span>
          </button>
        ))}
      </div>
    </div>
  );
}

function hoje(): string {
  return new Date().toISOString().slice(0, 10);
}
