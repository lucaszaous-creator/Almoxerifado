import { redirect } from "next/navigation";
import Link from "next/link";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { podeGerenciar, type Item } from "@/lib/tipos";
import { fmtNumero, fmtMoeda } from "@/lib/format";

export const dynamic = "force-dynamic";

export default async function CurvaAbcPage() {
  const perfil = await obterPerfilOuRedirecionar();
  if (!podeGerenciar(perfil.papel)) redirect("/");

  const supabase = criarClienteServidor();
  const { data } = await supabase.from("itens").select("*, categorias(nome)").eq("ativo", true);
  const itens = (data ?? []) as Item[];

  const comValor = itens
    .map((i) => ({ i, valor: i.quantidade * (i.custo_medio || i.preco_custo) }))
    .sort((a, b) => b.valor - a.valor);
  const total = comValor.reduce((s, x) => s + x.valor, 0) || 1;

  let acumulado = 0;
  const classificados = comValor.map((x) => {
    acumulado += x.valor;
    const pctAcum = (acumulado / total) * 100;
    const classe = pctAcum <= 80 ? "A" : pctAcum <= 95 ? "B" : "C";
    return { ...x, pctAcum, classe };
  });

  const resumo = { A: { n: 0, v: 0 }, B: { n: 0, v: 0 }, C: { n: 0, v: 0 } } as Record<
    string,
    { n: number; v: number }
  >;
  classificados.forEach((c) => {
    resumo[c.classe].n += 1;
    resumo[c.classe].v += c.valor;
  });

  const COR: Record<string, string> = {
    A: "bg-rose-100 text-rose-700",
    B: "bg-amber-100 text-amber-700",
    C: "bg-emerald-100 text-emerald-700",
  };

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div>
        <h1 className="page-title">Curva ABC</h1>
        <p className="text-slate-500">
          Classificação dos itens por valor em estoque. <b>A</b> = maior impacto (80% do valor),{" "}
          <b>B</b> = intermediário, <b>C</b> = menor impacto.
        </p>
      </div>

      <div className="grid grid-cols-3 gap-4">
        {(["A", "B", "C"] as const).map((cl) => (
          <div key={cl} className="card p-4">
            <span className={`badge ${COR[cl]}`}>Classe {cl}</span>
            <p className="mt-2 text-2xl font-bold text-slate-800">{resumo[cl].n} itens</p>
            <p className="text-xs text-slate-500">{fmtMoeda(resumo[cl].v)}</p>
          </div>
        ))}
      </div>

      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="th">Classe</th>
                <th className="th">Item</th>
                <th className="th text-right">Qtd.</th>
                <th className="th text-right">Valor</th>
                <th className="th text-right">% acum.</th>
              </tr>
            </thead>
            <tbody>
              {classificados.map(({ i, valor, pctAcum, classe }) => (
                <tr key={i.id} className="border-t border-slate-100">
                  <td className="px-4 py-2.5">
                    <span className={`badge ${COR[classe]}`}>{classe}</span>
                  </td>
                  <td className="px-4 py-2.5">
                    <Link href={`/itens/${i.id}`} className="font-medium text-slate-800 hover:text-brand-600">
                      {i.nome}
                    </Link>
                  </td>
                  <td className="px-4 py-2.5 text-right text-slate-600">
                    {fmtNumero(i.quantidade)} {i.unidade}
                  </td>
                  <td className="px-4 py-2.5 text-right font-medium text-slate-800">{fmtMoeda(valor)}</td>
                  <td className="px-4 py-2.5 text-right text-slate-500">{pctAcum.toFixed(1)}%</td>
                </tr>
              ))}
              {classificados.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-8 text-center text-slate-400">
                    Cadastre itens com custo para ver a curva ABC.
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
