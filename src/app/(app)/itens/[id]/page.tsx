import Link from "next/link";
import { notFound } from "next/navigation";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { fmtNumero, fmtMoeda, fmtData, fmtDataHora, diasAteValidade } from "@/lib/format";
import { podeGerenciar, type Fornecedor, type FornecedorItem, type Item, type Lote, type Movimentacao } from "@/lib/tipos";
import MovimentarEstoque from "@/components/MovimentarEstoque";
import GestaoLotes from "@/components/GestaoLotes";
import PrecosFornecedor from "@/components/PrecosFornecedor";
import PesquisaPrecos from "@/components/PesquisaPrecos";
import { pontoReposicaoSugerido, estoqueMaximoSugerido } from "@/lib/estoque";

export const dynamic = "force-dynamic";

const ROTULO_MOV = { entrada: "Entrada", saida: "Saída", ajuste: "Ajuste" };
const COR_MOV = {
  entrada: "bg-emerald-100 text-emerald-700",
  saida: "bg-rose-100 text-rose-700",
  ajuste: "bg-slate-100 text-slate-600",
};

export default async function ItemDetalhePage({ params }: { params: { id: string } }) {
  const perfil = await obterPerfilOuRedirecionar();
  const supabase = criarClienteServidor();

  const gestor = podeGerenciar(perfil.papel);

  // 1ª rodada em paralelo: item + histórico + (fornecedores/preços/consumo p/ gestor)
  const [itemRes, movsRes, precosRes, fornecedoresRes, consumoRes] = await Promise.all([
    supabase.from("itens").select("*, categorias(nome), fornecedores(nome)").eq("id", params.id).single(),
    supabase.from("movimentacoes").select("*, profiles(nome)").eq("item_id", params.id).order("criado_em", { ascending: false }).limit(50),
    gestor
      ? supabase.from("fornecedor_itens").select("*, fornecedores(nome)").eq("item_id", params.id)
      : Promise.resolve({ data: [] }),
    gestor ? supabase.from("fornecedores").select("*").order("nome") : Promise.resolve({ data: [] }),
    gestor ? supabase.rpc("consumo_dia_item", { p_item: params.id, p_dias: 60 }) : Promise.resolve({ data: null }),
  ]);

  const item = itemRes.data;
  if (!item) notFound();
  const i = item as Item;
  const movs = movsRes.data;
  const precos = precosRes.data;
  const fornecedores = fornecedoresRes.data;
  const consumoDia = Number(consumoRes.data ?? 0);
  const diasCobertura = consumoDia > 0 ? Math.round((i.quantidade / consumoDia) * 10) / 10 : null;
  const pontoSugerido = pontoReposicaoSugerido(consumoDia);
  const maximoSugerido = estoqueMaximoSugerido(consumoDia);

  const { data: lotes } = gestor && i.controla_lote
    ? await supabase.from("lotes").select("*, fornecedores(nome)").eq("item_id", params.id)
    : { data: [] };
  const baixo = i.estoque_minimo > 0 && i.quantidade <= i.estoque_minimo;
  const d = diasAteValidade(i.data_validade);

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <Link href="/itens" className="text-sm text-slate-500 hover:text-brand-600">
        ← Voltar para itens
      </Link>

      <div className="card p-6">
        <div className="flex flex-wrap items-start justify-between gap-3">
          <div className="flex gap-4">
            {i.imagem_url && (
              // eslint-disable-next-line @next/next/no-img-element
              <img
                src={i.imagem_url}
                alt={i.nome}
                className="h-20 w-20 shrink-0 rounded-xl border border-slate-200 object-cover"
              />
            )}
            <div>
              <div className="flex items-center gap-2">
                <h1 className="page-title">{i.nome}</h1>
                {!i.ativo && <span className="badge bg-slate-200 text-slate-500">Inativo</span>}
              </div>
              <p className="text-sm text-slate-400">
                cód. {i.codigo}
                {i.codigo_barras ? ` · barras ${i.codigo_barras}` : ""} · {i.categorias?.nome ?? "sem categoria"}
              </p>
              {(i.marca || i.fabricante) && (
                <p className="text-sm text-slate-500">
                  {[i.marca, i.fabricante].filter(Boolean).join(" · ")}
                </p>
              )}
              {i.fornecedores?.nome && <p className="text-sm text-slate-500">🏭 {i.fornecedores.nome}</p>}
            </div>
          </div>
          <div className="flex gap-2">
            <Link href={`/itens/${i.id}/kardex`} className="btn-secondary">
              📄 Kardex
            </Link>
            {gestor && (
              <Link href={`/itens/${i.id}/editar`} className="btn-secondary">
                ✏️ Editar
              </Link>
            )}
          </div>
        </div>

        {i.descricao && <p className="mt-3 text-slate-600">{i.descricao}</p>}

        <div className="mt-5 grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-6">
          <Info rotulo="Em estoque">
            <span className={baixo ? "text-amber-600" : "text-slate-800"}>
              {fmtNumero(i.quantidade)} {i.unidade}
            </span>
          </Info>
          <Info rotulo="Mínimo / Reposição">
            {fmtNumero(i.estoque_minimo)} / {fmtNumero(i.ponto_reposicao)}
          </Info>
          <Info rotulo="Estoque máximo">
            {i.estoque_maximo > 0 ? fmtNumero(i.estoque_maximo) : "—"}
          </Info>
          <Info rotulo="Custo médio">{fmtMoeda(i.custo_medio || i.preco_custo)}</Info>
          <Info rotulo="Valor em estoque">{fmtMoeda(i.quantidade * (i.custo_medio || i.preco_custo))}</Info>
          {gestor && (
            <Info rotulo="Cobertura (consumo)">
              {diasCobertura !== null ? (
                <span className={diasCobertura <= 7 ? "text-rose-600" : diasCobertura <= 21 ? "text-amber-600" : "text-slate-800"}>
                  ~{fmtNumero(diasCobertura)} dias
                </span>
              ) : (
                "—"
              )}
            </Info>
          )}
          <Info rotulo="Validade">
            {i.data_validade ? (
              <span className={d !== null && d < 0 ? "text-rose-600" : "text-slate-800"}>
                {fmtData(i.data_validade)}
              </span>
            ) : (
              "—"
            )}
          </Info>
        </div>

        {(i.localizacao || i.observacoes_internas) && (
          <div className="mt-4 space-y-1 text-sm text-slate-600">
            {i.localizacao && <p>📍 {i.localizacao}</p>}
            {gestor && i.observacoes_internas && <p className="text-slate-500">📝 {i.observacoes_internas}</p>}
          </div>
        )}

        <div className="mt-4 flex flex-wrap gap-2">
          {baixo && <span className="badge bg-amber-100 text-amber-700">⚠️ Estoque baixo</span>}
          {d !== null && d < 0 && <span className="badge bg-rose-100 text-rose-700">❌ Vencido</span>}
          {d !== null && d >= 0 && d <= 30 && (
            <span className="badge bg-orange-100 text-orange-700">⏳ Vence em {d} dias</span>
          )}
        </div>
      </div>

      {gestor && consumoDia > 0 && (pontoSugerido !== i.ponto_reposicao || maximoSugerido !== i.estoque_maximo) && (
        <div className="card border-brand-200 bg-brand-50/40 p-4">
          <div className="flex items-start gap-3">
            <span className="text-xl">💡</span>
            <div className="text-sm">
              <p className="font-semibold text-slate-800">Sugestão inteligente (com base no consumo)</p>
              <p className="mt-1 text-slate-600">
                Ponto de reposição sugerido: <b>{fmtNumero(pontoSugerido)} {i.unidade}</b>
                {" · "}Estoque máximo sugerido: <b>{fmtNumero(maximoSugerido)} {i.unidade}</b>.
              </p>
              <p className="mt-1 text-xs text-slate-400">
                Atual: reposição {fmtNumero(i.ponto_reposicao)} · máximo {fmtNumero(i.estoque_maximo)}.
                Ajuste em “Editar” se fizer sentido.
              </p>
            </div>
          </div>
        </div>
      )}

      {gestor && i.controla_lote && (
        <GestaoLotes
          item={i}
          lotes={(lotes ?? []) as Lote[]}
          fornecedores={(fornecedores ?? []) as Fornecedor[]}
          usuarioId={perfil.id}
        />
      )}

      {gestor && !i.controla_lote && <MovimentarEstoque item={i} usuarioId={perfil.id} />}

      {gestor && (
        <PrecosFornecedor
          itemId={i.id}
          fornecedores={(fornecedores ?? []) as Fornecedor[]}
          precos={(precos ?? []) as FornecedorItem[]}
        />
      )}

      {gestor && (
        <PesquisaPrecos
          produto={{
            nome: i.nome,
            marca: i.marca,
            fabricante: i.fabricante,
            codigo_barras: i.codigo_barras,
          }}
        />
      )}

      <div className="card">
        <div className="border-b border-slate-100 px-4 py-3">
          <h2 className="font-semibold text-slate-800">Histórico de movimentações</h2>
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
              <tr>
                <th className="px-4 py-2 font-medium">Data</th>
                <th className="px-4 py-2 font-medium">Tipo</th>
                <th className="px-4 py-2 text-right font-medium">Qtd.</th>
                <th className="px-4 py-2 font-medium">Motivo</th>
                <th className="px-4 py-2 font-medium">Responsável</th>
                <th className="px-4 py-2 font-medium">Observação</th>
              </tr>
            </thead>
            <tbody>
              {((movs ?? []) as Movimentacao[]).map((m) => (
                <tr key={m.id} className="border-t border-slate-100">
                  <td className="whitespace-nowrap px-4 py-2 text-slate-500">{fmtDataHora(m.criado_em)}</td>
                  <td className="px-4 py-2">
                    <span className={`badge ${COR_MOV[m.tipo]}`}>{ROTULO_MOV[m.tipo]}</span>
                  </td>
                  <td className="px-4 py-2 text-right font-medium text-slate-800">
                    {fmtNumero(m.quantidade)}
                  </td>
                  <td className="px-4 py-2 text-slate-500">{m.motivo ?? "—"}</td>
                  <td className="px-4 py-2 text-slate-600">{m.profiles?.nome ?? "—"}</td>
                  <td className="px-4 py-2 text-slate-500">{m.observacao ?? "—"}</td>
                </tr>
              ))}
              {(movs ?? []).length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-8 text-center text-slate-400">
                    Nenhuma movimentação registrada.
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

function Info({ rotulo, children }: { rotulo: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="text-xs uppercase text-slate-400">{rotulo}</p>
      <p className="mt-0.5 font-semibold">{children}</p>
    </div>
  );
}
