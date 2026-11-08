import Link from "next/link";
import { notFound } from "next/navigation";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { fmtNumero, fmtMoeda, fmtDataHora } from "@/lib/format";
import type { Item, Movimentacao } from "@/lib/tipos";

export const dynamic = "force-dynamic";

const ROTULO_MOV: Record<string, string> = { entrada: "Entrada", saida: "Saída", ajuste: "Ajuste" };
const COR_MOV: Record<string, string> = {
  entrada: "bg-emerald-100 text-emerald-700",
  saida: "bg-rose-100 text-rose-700",
  ajuste: "bg-slate-100 text-slate-600",
};

export default async function KardexPage({ params }: { params: { id: string } }) {
  await obterPerfilOuRedirecionar();
  const supabase = criarClienteServidor();

  const { data: item } = await supabase.from("itens").select("*").eq("id", params.id).single();
  if (!item) notFound();
  const i = item as Item;

  const { data } = await supabase
    .from("movimentacoes")
    .select("*, profiles(nome)")
    .eq("item_id", params.id)
    .order("criado_em", { ascending: true });
  const movs = (data ?? []) as Movimentacao[];

  // saldo corrido
  let saldo = 0;
  const linhas = movs.map((m) => {
    if (m.tipo === "entrada") saldo += m.quantidade;
    else if (m.tipo === "saida") saldo -= m.quantidade;
    else saldo = m.quantidade;
    return { m, saldo };
  });
  linhas.reverse();

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <Link href={`/itens/${i.id}`} className="text-sm text-slate-500 hover:text-brand-600">
        ← Voltar ao item
      </Link>
      <div>
        <h1 className="page-title">Kardex — {i.nome}</h1>
        <p className="text-slate-500">
          Ficha de estoque com saldo corrido. Saldo atual:{" "}
          <span className="font-semibold text-slate-700">
            {fmtNumero(i.quantidade)} {i.unidade}
          </span>{" "}
          · Custo médio {fmtMoeda(i.custo_medio || i.preco_custo)}
        </p>
      </div>

      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="th">Data</th>
                <th className="th">Tipo</th>
                <th className="th">Motivo</th>
                <th className="th text-right">Qtd.</th>
                <th className="th text-right">Custo unit.</th>
                <th className="th text-right">Saldo</th>
                <th className="th">Responsável</th>
              </tr>
            </thead>
            <tbody>
              {linhas.map(({ m, saldo }) => (
                <tr key={m.id} className="border-t border-slate-100">
                  <td className="whitespace-nowrap px-4 py-2 text-slate-500">{fmtDataHora(m.criado_em)}</td>
                  <td className="px-4 py-2">
                    <span className={`badge ${COR_MOV[m.tipo]}`}>{ROTULO_MOV[m.tipo]}</span>
                  </td>
                  <td className="px-4 py-2 text-slate-500">{m.motivo ?? "—"}</td>
                  <td className="px-4 py-2 text-right text-slate-700">{fmtNumero(m.quantidade)}</td>
                  <td className="px-4 py-2 text-right text-slate-500">
                    {m.custo_unitario != null ? fmtMoeda(m.custo_unitario) : "—"}
                  </td>
                  <td className="px-4 py-2 text-right font-medium text-slate-800">{fmtNumero(saldo)}</td>
                  <td className="px-4 py-2 text-slate-600">{m.profiles?.nome ?? "—"}</td>
                </tr>
              ))}
              {linhas.length === 0 && (
                <tr>
                  <td colSpan={7} className="px-4 py-8 text-center text-slate-400">
                    Nenhuma movimentação.
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
