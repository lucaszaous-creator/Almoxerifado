import { redirect } from "next/navigation";
import Link from "next/link";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { podeGerenciar, type Item } from "@/lib/tipos";
import { fmtNumero, fmtData, diasAteValidade } from "@/lib/format";

export const dynamic = "force-dynamic";

export default async function AtencoesPage() {
  const perfil = await obterPerfilOuRedirecionar();
  if (!podeGerenciar(perfil.papel)) redirect("/");

  const supabase = criarClienteServidor();
  const { data: cfg } = await supabase
    .from("configuracoes")
    .select("dias_alerta_validade")
    .eq("id", true)
    .single();
  const diasAlerta = cfg?.dias_alerta_validade ?? 30;

  const [, itensRes] = await Promise.all([
    supabase.rpc("gerar_alertas_validade", { p_dias: diasAlerta }),
    supabase.from("itens").select("*, categorias(nome)").eq("ativo", true).order("nome"),
  ]);
  const itens = (itensRes.data ?? []) as Item[];

  const estoqueBaixo = itens.filter((i) => i.estoque_minimo > 0 && i.quantidade <= i.estoque_minimo);
  const semEstoque = estoqueBaixo.filter((i) => i.quantidade <= 0);
  const vencidos = itens.filter((i) => {
    const d = diasAteValidade(i.data_validade);
    return d !== null && d < 0;
  });
  const vencendo = itens.filter((i) => {
    const d = diasAteValidade(i.data_validade);
    return d !== null && d >= 0 && d <= 30;
  });

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="page-title">Atenções</h1>
        <p className="text-slate-500">
          Tudo que precisa de ação: itens acabando e validades críticas.
        </p>
      </div>

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <Kpi titulo="Sem estoque" valor={semEstoque.length} cor="rose" />
        <Kpi titulo="Estoque baixo" valor={estoqueBaixo.length} cor="amber" />
        <Kpi titulo="Vencidos" valor={vencidos.length} cor="rose" />
        <Kpi titulo="Vencendo (30d)" valor={vencendo.length} cor="orange" />
      </div>

      <Grupo
        titulo="Itens sem estoque ou abaixo do mínimo"
        icone="⚠️"
        vazio="Nenhum item abaixo do mínimo. 🎉"
      >
        {estoqueBaixo.map((i) => (
          <LinhaItem key={i.id} item={i}>
            <span
              className={`badge ${i.quantidade <= 0 ? "bg-rose-100 text-rose-700" : "bg-amber-100 text-amber-700"}`}
            >
              {fmtNumero(i.quantidade)} / mín {fmtNumero(i.estoque_minimo)} {i.unidade}
            </span>
            <Link
              href={`/compras?item=${i.id}`}
              className="text-xs font-medium text-brand-600 hover:underline"
            >
              Comprar
            </Link>
          </LinhaItem>
        ))}
      </Grupo>

      <Grupo titulo="Itens vencidos" icone="❌" vazio="Nenhum item vencido.">
        {vencidos.map((i) => {
          const d = diasAteValidade(i.data_validade)!;
          return (
            <LinhaItem key={i.id} item={i}>
              <span className="badge bg-rose-100 text-rose-700">
                venceu há {-d}d ({fmtData(i.data_validade)})
              </span>
            </LinhaItem>
          );
        })}
      </Grupo>

      <Grupo titulo="Validade próxima (até 30 dias)" icone="⏳" vazio="Nenhuma validade próxima.">
        {vencendo.map((i) => {
          const d = diasAteValidade(i.data_validade)!;
          return (
            <LinhaItem key={i.id} item={i}>
              <span className="badge bg-orange-100 text-orange-700">
                vence em {d}d ({fmtData(i.data_validade)})
              </span>
            </LinhaItem>
          );
        })}
      </Grupo>
    </div>
  );
}

function Kpi({ titulo, valor, cor }: { titulo: string; valor: number; cor: string }) {
  const cores: Record<string, string> = {
    rose: "text-rose-600",
    amber: "text-amber-600",
    orange: "text-orange-600",
  };
  return (
    <div className="card p-4">
      <p className={`text-3xl font-bold ${cores[cor] ?? "text-slate-800"}`}>{valor}</p>
      <p className="text-xs text-slate-500">{titulo}</p>
    </div>
  );
}

function Grupo({
  titulo,
  icone,
  vazio,
  children,
}: {
  titulo: string;
  icone: string;
  vazio: string;
  children: React.ReactNode;
}) {
  const temFilhos = Array.isArray(children) ? children.some(Boolean) : Boolean(children);
  return (
    <div className="card">
      <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
        <span>{icone}</span>
        <h2 className="font-semibold text-slate-800">{titulo}</h2>
      </div>
      {temFilhos ? (
        <div>{children}</div>
      ) : (
        <p className="px-4 py-8 text-center text-sm text-slate-400">{vazio}</p>
      )}
    </div>
  );
}

function LinhaItem({ item, children }: { item: Item; children: React.ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3 border-b border-slate-50 px-4 py-2.5">
      <Link href={`/itens/${item.id}`} className="min-w-0 flex-1 hover:text-brand-600">
        <p className="truncate text-sm font-medium text-slate-800">{item.nome}</p>
        <p className="text-xs text-slate-400">
          {item.categorias?.nome ?? "sem categoria"} · {item.localizacao || "sem local"}
        </p>
      </Link>
      <div className="flex items-center gap-3">{children}</div>
    </div>
  );
}
