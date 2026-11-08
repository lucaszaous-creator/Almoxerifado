import { redirect } from "next/navigation";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { podeGerenciar, type FornecedorItem } from "@/lib/tipos";
import MelhoresPrecos from "@/components/MelhoresPrecos";

export const dynamic = "force-dynamic";

export default async function MelhoresPrecosPage() {
  const perfil = await obterPerfilOuRedirecionar();
  if (!podeGerenciar(perfil.papel)) redirect("/");

  const supabase = criarClienteServidor();
  const { data } = await supabase
    .from("fornecedor_itens")
    .select(
      "*, fornecedores(nome, fornece_cnpj, cnpj), itens(nome, codigo, unidade, quantidade, estoque_minimo, ponto_reposicao, ativo)"
    );

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div>
        <h1 className="page-title">Melhores preços (CNPJ)</h1>
        <p className="text-slate-500">
          Digite o nome do produto para <b>pesquisar preços na internet</b> (Google, Shopping,
          Mercado Livre) e compare com os preços dos fornecedores CNPJ já cadastrados — gerando o
          pedido para o de menor preço.
        </p>
      </div>
      <MelhoresPrecos precos={(data ?? []) as FornecedorItem[]} usuarioId={perfil.id} />
    </div>
  );
}
