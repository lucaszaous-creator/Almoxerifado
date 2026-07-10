import { redirect } from "next/navigation";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { podeGerenciar, type Fornecedor } from "@/lib/tipos";
import GestaoFornecedores from "@/components/GestaoFornecedores";

export const dynamic = "force-dynamic";

export default async function FornecedoresPage() {
  const perfil = await obterPerfilOuRedirecionar();
  if (!podeGerenciar(perfil.papel)) redirect("/");

  const supabase = criarClienteServidor();
  const { data } = await supabase.from("fornecedores").select("*").order("nome");

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <div>
        <h1 className="page-title">Fornecedores</h1>
        <p className="text-slate-500">Cadastro de fornecedores para vincular a itens e compras.</p>
      </div>
      <GestaoFornecedores fornecedores={(data ?? []) as Fornecedor[]} />
    </div>
  );
}
