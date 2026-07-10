import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { podeGerenciar, precisaRepor, type Fornecedor, type Item, type PedidoCompra } from "@/lib/tipos";
import NovoPedido from "@/components/NovoPedido";
import ListaPedidos from "@/components/ListaPedidos";
import SugestoesReposicao from "@/components/SugestoesReposicao";

export const dynamic = "force-dynamic";

export default async function ComprasPage({
  searchParams,
}: {
  searchParams: { item?: string };
}) {
  const perfil = await obterPerfilOuRedirecionar();
  const supabase = criarClienteServidor();
  const gestor = podeGerenciar(perfil.papel);

  const { data: pedidos } = await supabase
    .from("pedidos_compra")
    .select("*, solicitante:profiles!pedidos_compra_solicitante_id_fkey(nome), fornecedores(nome)")
    .order("criado_em", { ascending: false });

  const { data: itens } = await supabase
    .from("itens")
    .select("id, nome, codigo, unidade, quantidade, estoque_minimo, ponto_reposicao, estoque_maximo, preco_custo, fornecedor_id")
    .eq("ativo", true)
    .order("nome");

  const { data: fornecedores } = await supabase.from("fornecedores").select("*").order("nome");

  const listaItens = (itens ?? []) as Item[];
  const sugestoes = gestor ? listaItens.filter((i) => precisaRepor(i)) : [];

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div>
        <h1 className="page-title">Compras</h1>
        <p className="text-slate-500">
          Solicite a compra de itens em falta. Gerente e almoxarife são notificados a cada pedido.
        </p>
      </div>

      <NovoPedido
        itens={listaItens}
        fornecedores={(fornecedores ?? []) as Fornecedor[]}
        usuarioId={perfil.id}
        itemInicial={searchParams.item}
      />

      {gestor && sugestoes.length > 0 && (
        <SugestoesReposicao itens={sugestoes} usuarioId={perfil.id} />
      )}

      <ListaPedidos
        pedidos={(pedidos ?? []) as PedidoCompra[]}
        podeGerir={gestor}
        usuarioId={perfil.id}
      />
    </div>
  );
}
