import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { podeGerenciar, type Item, type Requisicao } from "@/lib/tipos";
import NovaRequisicao from "@/components/NovaRequisicao";
import ListaRequisicoes from "@/components/ListaRequisicoes";

export const dynamic = "force-dynamic";

export default async function RequisicoesPage() {
  const perfil = await obterPerfilOuRedirecionar();
  const supabase = criarClienteServidor();

  const { data: requisicoes } = await supabase
    .from("requisicoes")
    .select(
      "*, solicitante:profiles!requisicoes_solicitante_id_fkey(nome), requisicao_itens(*, itens(nome, codigo, unidade, quantidade))"
    )
    .order("criado_em", { ascending: false });

  const { data: itens } = await supabase
    .from("itens")
    .select("id, nome, codigo, unidade, quantidade")
    .order("nome");

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div>
        <h1 className="page-title">Requisições de material</h1>
        <p className="text-slate-500">
          Solicite itens do estoque sem ir ao almoxarifado. O almoxarife aprova e atende — a baixa
          no estoque é automática.
        </p>
      </div>

      <NovaRequisicao itens={(itens ?? []) as Item[]} usuarioId={perfil.id} />

      <ListaRequisicoes
        requisicoes={(requisicoes ?? []) as Requisicao[]}
        podeGerir={podeGerenciar(perfil.papel)}
        usuarioId={perfil.id}
      />
    </div>
  );
}
