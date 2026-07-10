import { redirect } from "next/navigation";
import Link from "next/link";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { podeGerenciar, type Movimentacao } from "@/lib/tipos";
import { fmtNumero, fmtDataHora } from "@/lib/format";

export const dynamic = "force-dynamic";

const ROTULO_MOV = { entrada: "Entrada", saida: "Saída", ajuste: "Ajuste" };
const COR_MOV = {
  entrada: "bg-emerald-100 text-emerald-700",
  saida: "bg-rose-100 text-rose-700",
  ajuste: "bg-slate-100 text-slate-600",
};

export default async function MovimentacoesPage() {
  const perfil = await obterPerfilOuRedirecionar();
  if (!podeGerenciar(perfil.papel)) redirect("/");

  const supabase = criarClienteServidor();
  const { data } = await supabase
    .from("movimentacoes")
    .select("*, itens(nome, codigo, unidade), profiles(nome)")
    .order("criado_em", { ascending: false })
    .limit(200);

  const movs = (data ?? []) as Movimentacao[];

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div>
        <h1 className="page-title">Movimentações</h1>
        <p className="text-slate-500">Histórico geral de entradas, saídas e ajustes de estoque.</p>
      </div>

      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 font-medium">Data</th>
                <th className="px-4 py-3 font-medium">Item</th>
                <th className="px-4 py-3 font-medium">Tipo</th>
                <th className="px-4 py-3 text-right font-medium">Qtd.</th>
                <th className="px-4 py-3 font-medium">Responsável</th>
                <th className="px-4 py-3 font-medium">Observação</th>
              </tr>
            </thead>
            <tbody>
              {movs.map((m) => (
                <tr key={m.id} className="border-t border-slate-100 hover:bg-slate-50">
                  <td className="whitespace-nowrap px-4 py-2.5 text-slate-500">{fmtDataHora(m.criado_em)}</td>
                  <td className="px-4 py-2.5">
                    <Link href={`/itens/${m.item_id}`} className="font-medium text-slate-800 hover:text-brand-600">
                      {m.itens?.nome ?? "item removido"}
                    </Link>
                  </td>
                  <td className="px-4 py-2.5">
                    <span className={`badge ${COR_MOV[m.tipo]}`}>{ROTULO_MOV[m.tipo]}</span>
                  </td>
                  <td className="px-4 py-2.5 text-right font-medium text-slate-800">
                    {fmtNumero(m.quantidade)} {m.itens?.unidade ?? ""}
                  </td>
                  <td className="px-4 py-2.5 text-slate-600">{m.profiles?.nome ?? "—"}</td>
                  <td className="px-4 py-2.5 text-slate-500">{m.observacao ?? "—"}</td>
                </tr>
              ))}
              {movs.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-10 text-center text-slate-400">
                    Nenhuma movimentação ainda.
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
