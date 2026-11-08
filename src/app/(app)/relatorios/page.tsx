import { redirect } from "next/navigation";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { podeGerenciar, type Item } from "@/lib/tipos";
import { fmtNumero, fmtMoeda } from "@/lib/format";
import ExportarRelatorios from "@/components/ExportarRelatorios";

export const dynamic = "force-dynamic";

export default async function RelatoriosPage() {
  const perfil = await obterPerfilOuRedirecionar();
  if (!podeGerenciar(perfil.papel)) redirect("/");

  const supabase = criarClienteServidor();
  const { data } = await supabase.from("itens").select("*, categorias(nome)").order("nome");
  const itens = (data ?? []) as Item[];

  const totalItens = itens.length;
  const baixo = itens.filter((i) => i.estoque_minimo > 0 && i.quantidade <= i.estoque_minimo).length;
  const semEstoque = itens.filter((i) => i.quantidade <= 0).length;

  const valorEstoque = itens.reduce((s, i) => s + i.quantidade * (i.preco_custo ?? 0), 0);

  // resumo por categoria
  const resumo = new Map<string, { itens: number; unidades: number; valor: number }>();
  for (const i of itens) {
    const nome = i.categorias?.nome ?? "Sem categoria";
    const cur = resumo.get(nome) ?? { itens: 0, unidades: 0, valor: 0 };
    cur.itens += 1;
    cur.unidades += Number(i.quantidade);
    cur.valor += Number(i.quantidade) * (Number(i.preco_custo) || 0);
    resumo.set(nome, cur);
  }
  const linhasResumo = [...resumo.entries()].sort((a, b) => b[1].valor - a[1].valor);

  return (
    <div className="mx-auto max-w-4xl space-y-6">
      <div>
        <h1 className="page-title">Relatórios</h1>
        <p className="text-slate-500">Exporte dados em CSV (abre no Excel) e veja o resumo do estoque.</p>
      </div>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <div className="card p-4">
          <p className="text-2xl font-bold text-brand-700">{fmtMoeda(valorEstoque)}</p>
          <p className="text-xs text-slate-500">Valor em estoque</p>
        </div>
        <div className="card p-4">
          <p className="text-3xl font-bold text-slate-800">{totalItens}</p>
          <p className="text-xs text-slate-500">Itens cadastrados</p>
        </div>
        <div className="card p-4">
          <p className="text-3xl font-bold text-amber-600">{baixo}</p>
          <p className="text-xs text-slate-500">Estoque baixo</p>
        </div>
        <div className="card p-4">
          <p className="text-3xl font-bold text-rose-600">{semEstoque}</p>
          <p className="text-xs text-slate-500">Sem estoque</p>
        </div>
      </div>

      <ExportarRelatorios />

      <div className="card overflow-hidden">
        <div className="border-b border-slate-100 px-4 py-3">
          <h2 className="font-semibold text-slate-800">Resumo por categoria</h2>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-3 font-medium">Categoria</th>
                <th className="px-4 py-3 text-right font-medium">Itens</th>
                <th className="px-4 py-3 text-right font-medium">Unidades</th>
                <th className="px-4 py-3 text-right font-medium">Valor</th>
              </tr>
            </thead>
            <tbody>
              {linhasResumo.map(([nome, r]) => (
                <tr key={nome} className="border-t border-slate-100">
                  <td className="px-4 py-2.5 text-slate-700">{nome}</td>
                  <td className="px-4 py-2.5 text-right font-medium text-slate-800">{r.itens}</td>
                  <td className="px-4 py-2.5 text-right text-slate-600">{fmtNumero(r.unidades)}</td>
                  <td className="px-4 py-2.5 text-right text-slate-600">{fmtMoeda(r.valor)}</td>
                </tr>
              ))}
              {linhasResumo.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-4 py-8 text-center text-slate-400">
                    Nenhum item cadastrado.
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
