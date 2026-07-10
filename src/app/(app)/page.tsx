import Link from "next/link";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { fmtNumero, fmtMoeda, fmtData, fmtDataHora, diasAteValidade } from "@/lib/format";
import { podeGerenciar, type Item, type Movimentacao } from "@/lib/tipos";

export const dynamic = "force-dynamic";

const COR_MOV: Record<string, string> = {
  entrada: "bg-emerald-100 text-emerald-700",
  saida: "bg-rose-100 text-rose-700",
  ajuste: "bg-slate-100 text-slate-600",
};
const ROTULO_MOV: Record<string, string> = { entrada: "Entrada", saida: "Saída", ajuste: "Ajuste" };

export default async function PainelPage() {
  const perfil = await obterPerfilOuRedirecionar();
  const supabase = criarClienteServidor();
  const gestor = podeGerenciar(perfil.papel);

  const { data: cfg } = await supabase
    .from("configuracoes")
    .select("dias_alerta_validade")
    .eq("id", true)
    .single();
  const diasAlerta = cfg?.dias_alerta_validade ?? 30;

  // Todas as consultas em paralelo
  const [, itensRes, comprasRes, reqsRes, movsRes, resumoRes, previsaoRes] = await Promise.all([
    supabase.rpc("gerar_alertas_validade", { p_dias: diasAlerta }),
    supabase
      .from("itens")
      .select("id, nome, quantidade, estoque_minimo, data_validade, preco_custo, custo_medio, localizacao, categorias(nome)")
      .order("nome"),
    supabase.from("pedidos_compra").select("*", { count: "exact", head: true }).eq("status", "pendente"),
    supabase.from("requisicoes").select("*", { count: "exact", head: true }).eq("status", "pendente"),
    supabase.from("movimentacoes").select("id, item_id, tipo, quantidade, criado_em, itens(nome, unidade)").order("criado_em", { ascending: false }).limit(8),
    gestor ? supabase.rpc("mov_resumo_30d") : Promise.resolve({ data: null }),
    gestor ? supabase.rpc("previsao_consumo", { p_dias: 60 }) : Promise.resolve({ data: [] }),
  ]);

  const lista = (itensRes.data ?? []) as unknown as Item[];
  const comprasPendentes = comprasRes.count;
  const reqsPendentes = reqsRes.count;
  const movs = movsRes.data;

  const estoqueBaixo = lista.filter((i) => i.estoque_minimo > 0 && i.quantidade <= i.estoque_minimo);
  const vencendo = lista.filter((i) => {
    const d = diasAteValidade(i.data_validade);
    return d !== null && d >= 0 && d <= diasAlerta;
  });
  const vencidos = lista.filter((i) => {
    const d = diasAteValidade(i.data_validade);
    return d !== null && d < 0;
  });

  // Distribuição por categoria (top 6)
  const porCategoria = new Map<string, number>();
  for (const i of lista) {
    const nome = i.categorias?.nome ?? "Sem categoria";
    porCategoria.set(nome, (porCategoria.get(nome) ?? 0) + 1);
  }
  const categorias = [...porCategoria.entries()].sort((a, b) => b[1] - a[1]).slice(0, 6);
  const maxCat = Math.max(1, ...categorias.map(([, n]) => n));

  const valorEstoque = lista.reduce((s, i) => s + i.quantidade * (i.custo_medio || i.preco_custo || 0), 0);

  // Consumo (30 dias) — vem agregado do banco (RPC)
  type Resumo = {
    entradas30: number;
    saidas30: number;
    serie14: { d: string; ent: number; sai: number }[];
    top: { nome: string; unidade: string; qtd: number }[];
  };
  const resumo = (resumoRes.data ?? { entradas30: 0, saidas30: 0, serie14: [], top: [] }) as Resumo;
  const totEntradas = Number(resumo.entradas30) || 0;
  const totSaidas = Number(resumo.saidas30) || 0;
  const topConsumidos = (resumo.top ?? []).map((t) => ({ ...t, qtd: Number(t.qtd) }));
  const maxConsumo = Math.max(1, ...topConsumidos.map((t) => t.qtd));
  const serie = (resumo.serie14 ?? []).map((x) => ({
    dia: Number(x.d.slice(8, 10)),
    ent: Number(x.ent),
    sai: Number(x.sai),
  }));
  const maxSerie = Math.max(1, ...serie.map((s) => Math.max(s.ent, s.sai)));

  // Previsão: itens que vão acabar em breve (por consumo médio)
  type Prev = { item_id: string; nome: string; unidade: string; quantidade: number; consumo_dia: number; dias_cobertura: number | null };
  const previsao = ((previsaoRes.data ?? []) as Prev[])
    .filter((p) => p.dias_cobertura !== null && p.dias_cobertura <= 21)
    .slice(0, 6);

  return (
    <div className="mx-auto max-w-6xl space-y-6">
      <div>
        <h1 className="page-title">Olá, {perfil.nome.split(" ")[0]} 👋</h1>
        <p className="page-sub">Visão geral do almoxarifado</p>
      </div>

      {/* Ações rápidas */}
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        <AcaoRapida href="/scanner" icone="📷" titulo="Escanear" />
        <AcaoRapida href="/itens" icone="🔍" titulo="Consultar" />
        <AcaoRapida href="/requisicoes" icone="📝" titulo="Requisitar" />
        <AcaoRapida href="/compras" icone="🛒" titulo="Comprar" />
      </div>

      {gestor && (
        <div className="flex flex-wrap items-center justify-between gap-3 overflow-hidden rounded-2xl bg-gradient-to-br from-brand-600 via-brand-700 to-brand-800 p-5 text-white shadow-md shadow-brand-900/20">
          <div>
            <p className="text-sm text-brand-100">Valor total em estoque</p>
            <p className="text-3xl font-bold tracking-tight">{fmtMoeda(valorEstoque)}</p>
          </div>
          <div className="text-right">
            <p className="text-sm text-brand-100">{lista.length} itens cadastrados</p>
            <Link href="/relatorios" className="text-sm font-medium text-white/90 underline underline-offset-2 hover:text-white">
              Ver relatórios
            </Link>
          </div>
        </div>
      )}

      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-5">
        <CardKpi titulo="Itens cadastrados" valor={lista.length} icone="📦" cor="brand" href="/itens" />
        <CardKpi titulo="Estoque baixo" valor={estoqueBaixo.length} icone="⚠️" cor="amber" href="/itens?filtro=baixo" />
        <CardKpi titulo="Validade crítica" valor={vencendo.length + vencidos.length} icone="⏳" cor="rose" href="/itens?filtro=validade" />
        <CardKpi titulo="Compras pendentes" valor={comprasPendentes ?? 0} icone="🛒" cor="emerald" href="/compras" />
        <CardKpi titulo="Requisições pendentes" valor={reqsPendentes ?? 0} icone="📝" cor="indigo" href="/requisicoes" />
      </div>

      {gestor && previsao.length > 0 && (
        <div className="card overflow-hidden">
          <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
            <div className="flex items-center gap-2">
              <span>🔮</span>
              <h2 className="font-semibold text-slate-800">Vão acabar em breve (por consumo)</h2>
            </div>
            <Link href="/compras" className="text-sm font-medium text-brand-600 hover:underline">
              Repor
            </Link>
          </div>
          <div>
            {previsao.map((p) => (
              <Link
                key={p.item_id}
                href={`/itens/${p.item_id}`}
                className="flex items-center justify-between gap-3 border-b border-slate-50 px-4 py-2.5 hover:bg-slate-50"
              >
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium text-slate-800">{p.nome}</p>
                  <p className="text-xs text-slate-400">
                    consumo ~{fmtNumero(p.consumo_dia)} {p.unidade}/dia · resta {fmtNumero(p.quantidade)} {p.unidade}
                  </p>
                </div>
                <span
                  className={`badge shrink-0 ${
                    (p.dias_cobertura ?? 0) <= 7 ? "bg-rose-100 text-rose-700" : "bg-amber-100 text-amber-700"
                  }`}
                >
                  ~{p.dias_cobertura} dias
                </span>
              </Link>
            ))}
          </div>
        </div>
      )}

      <div className="grid gap-6 lg:grid-cols-2">
        <div className="card">
          <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
            <span>📚</span>
            <h2 className="font-semibold text-slate-800">Itens por categoria</h2>
          </div>
          <div className="space-y-3 p-4">
            {categorias.length === 0 && (
              <p className="py-6 text-center text-sm text-slate-400">Nenhum item cadastrado.</p>
            )}
            {categorias.map(([nome, n]) => (
              <div key={nome}>
                <div className="mb-1 flex justify-between text-sm">
                  <span className="text-slate-600">{nome}</span>
                  <span className="font-medium text-slate-800">{n}</span>
                </div>
                <div className="h-2 overflow-hidden rounded-full bg-slate-100">
                  <div
                    className="h-full rounded-full bg-brand-500"
                    style={{ width: `${(n / maxCat) * 100}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        </div>

        {gestor ? (
          <div className="card">
            <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
              <span>🔄</span>
              <h2 className="font-semibold text-slate-800">Movimentações recentes</h2>
            </div>
            <div>
              {((movs ?? []) as unknown as Movimentacao[]).map((m) => (
                <Link
                  key={m.id}
                  href={`/itens/${m.item_id}`}
                  className="flex items-center justify-between border-b border-slate-50 px-4 py-2.5 hover:bg-slate-50"
                >
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium text-slate-800">
                      {m.itens?.nome ?? "item"}
                    </p>
                    <p className="text-xs text-slate-400">{fmtDataHora(m.criado_em)}</p>
                  </div>
                  <span className={`badge ${COR_MOV[m.tipo]}`}>
                    {ROTULO_MOV[m.tipo]} {fmtNumero(m.quantidade)} {m.itens?.unidade}
                  </span>
                </Link>
              ))}
              {(movs ?? []).length === 0 && (
                <p className="px-4 py-8 text-center text-sm text-slate-400">
                  Nenhuma movimentação ainda.
                </p>
              )}
            </div>
          </div>
        ) : (
          <div className="card">
            <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
              <span>⏳</span>
              <h2 className="font-semibold text-slate-800">Validade próxima ou vencida</h2>
            </div>
            <div>
              {[...vencidos, ...vencendo].slice(0, 8).map((i) => {
                const d = diasAteValidade(i.data_validade)!;
                return (
                  <Link
                    key={i.id}
                    href={`/itens/${i.id}`}
                    className="flex items-center justify-between border-b border-slate-50 px-4 py-2.5 hover:bg-slate-50"
                  >
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium text-slate-800">{i.nome}</p>
                      <p className="text-xs text-slate-400">Validade: {fmtData(i.data_validade)}</p>
                    </div>
                    <span className={`badge ${d < 0 ? "bg-rose-100 text-rose-700" : "bg-amber-100 text-amber-700"}`}>
                      {d < 0 ? `vencido há ${-d}d` : `vence em ${d}d`}
                    </span>
                  </Link>
                );
              })}
              {vencidos.length + vencendo.length === 0 && (
                <p className="px-4 py-8 text-center text-sm text-slate-400">
                  Nenhum item com validade crítica.
                </p>
              )}
            </div>
          </div>
        )}
      </div>

      {gestor && (
        <div className="grid gap-6 lg:grid-cols-2">
          <div className="card p-4">
            <div className="mb-3 flex items-center justify-between">
              <h2 className="font-semibold text-slate-800">Movimentação (14 dias)</h2>
              <div className="flex gap-3 text-xs">
                <span className="flex items-center gap-1 text-emerald-600">
                  <span className="h-2 w-2 rounded-full bg-emerald-500" /> entradas
                </span>
                <span className="flex items-center gap-1 text-rose-600">
                  <span className="h-2 w-2 rounded-full bg-rose-500" /> saídas
                </span>
              </div>
            </div>
            <div className="flex h-32 items-end gap-1">
              {serie.map((s, k) => (
                <div key={k} className="flex flex-1 flex-col items-center justify-end gap-0.5" title={`Dia ${s.dia}: +${s.ent} / -${s.sai}`}>
                  <div className="flex w-full items-end justify-center gap-0.5" style={{ height: "100%" }}>
                    <div className="w-1/2 rounded-t bg-emerald-400" style={{ height: `${(s.ent / maxSerie) * 100}%` }} />
                    <div className="w-1/2 rounded-t bg-rose-400" style={{ height: `${(s.sai / maxSerie) * 100}%` }} />
                  </div>
                  <span className="text-[10px] text-slate-400">{s.dia}</span>
                </div>
              ))}
            </div>
            <p className="mt-2 text-xs text-slate-500">
              30 dias: {fmtNumero(totEntradas)} entradas · {fmtNumero(totSaidas)} saídas
            </p>
          </div>

          <div className="card">
            <div className="flex items-center gap-2 border-b border-slate-100 px-4 py-3">
              <span>🔥</span>
              <h2 className="font-semibold text-slate-800">Mais consumidos (30 dias)</h2>
            </div>
            <div className="space-y-2 p-4">
              {topConsumidos.length === 0 && (
                <p className="py-6 text-center text-sm text-slate-400">Sem saídas no período.</p>
              )}
              {topConsumidos.map((t) => (
                <div key={t.nome}>
                  <div className="mb-1 flex justify-between text-sm">
                    <span className="truncate text-slate-600">{t.nome}</span>
                    <span className="font-medium text-slate-800">
                      {fmtNumero(t.qtd)} {t.unidade}
                    </span>
                  </div>
                  <div className="h-2 overflow-hidden rounded-full bg-slate-100">
                    <div className="h-full rounded-full bg-rose-400" style={{ width: `${(t.qtd / maxConsumo) * 100}%` }} />
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      <div className="card">
        <div className="flex items-center justify-between border-b border-slate-100 px-4 py-3">
          <div className="flex items-center gap-2">
            <span>⚠️</span>
            <h2 className="font-semibold text-slate-800">Itens com estoque baixo</h2>
          </div>
          <Link href="/itens?filtro=baixo" className="text-sm font-medium text-brand-600 hover:underline">
            Ver todos
          </Link>
        </div>
        <div>
          {estoqueBaixo.slice(0, 8).map((i) => (
            <Link
              key={i.id}
              href={`/itens/${i.id}`}
              className="flex items-center justify-between border-b border-slate-50 px-4 py-2.5 hover:bg-slate-50"
            >
              <div className="min-w-0">
                <p className="truncate text-sm font-medium text-slate-800">{i.nome}</p>
                <p className="text-xs text-slate-400">{i.localizacao || "sem local"}</p>
              </div>
              <span className="badge bg-amber-100 text-amber-700">
                {fmtNumero(i.quantidade)} / mín {fmtNumero(i.estoque_minimo)} {i.unidade}
              </span>
            </Link>
          ))}
          {estoqueBaixo.length === 0 && (
            <p className="px-4 py-8 text-center text-sm text-slate-400">
              Nenhum item abaixo do mínimo. 🎉
            </p>
          )}
        </div>
      </div>
    </div>
  );
}

function CardKpi({
  titulo,
  valor,
  icone,
  cor,
  href,
}: {
  titulo: string;
  valor: number;
  icone: string;
  cor: "brand" | "amber" | "rose" | "emerald" | "indigo";
  href: string;
}) {
  const cores = {
    brand: "bg-brand-50 text-brand-700",
    amber: "bg-amber-50 text-amber-700",
    rose: "bg-rose-50 text-rose-700",
    emerald: "bg-emerald-50 text-emerald-700",
    indigo: "bg-indigo-50 text-indigo-700",
  };
  return (
    <Link href={href} className="card card-hover p-4">
      <div className={`mb-2 inline-flex h-9 w-9 items-center justify-center rounded-lg text-lg ${cores[cor]}`}>
        {icone}
      </div>
      <p className="text-2xl font-bold tracking-tight text-slate-800">{valor}</p>
      <p className="text-xs text-slate-500">{titulo}</p>
    </Link>
  );
}

function AcaoRapida({ href, icone, titulo }: { href: string; icone: string; titulo: string }) {
  return (
    <Link
      href={href}
      className="card card-hover flex items-center gap-3 p-3 sm:flex-col sm:justify-center sm:py-4 sm:text-center"
    >
      <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-brand-50 text-xl text-brand-700">
        {icone}
      </span>
      <span className="text-sm font-semibold text-slate-700">{titulo}</span>
    </Link>
  );
}
