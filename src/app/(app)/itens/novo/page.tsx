import { redirect } from "next/navigation";
import Link from "next/link";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { podeGerenciar, type Categoria, type Fornecedor } from "@/lib/tipos";
import FormItem from "@/components/FormItem";
import GerenciarCategorias from "@/components/GerenciarCategorias";

export const dynamic = "force-dynamic";

export default async function NovoItemPage() {
  const perfil = await obterPerfilOuRedirecionar();
  if (!podeGerenciar(perfil.papel)) redirect("/itens");

  const supabase = criarClienteServidor();
  const { data: categorias } = await supabase.from("categorias").select("*").order("nome");
  const { data: fornecedores } = await supabase.from("fornecedores").select("*").order("nome");

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <Link href="/itens" className="text-sm text-slate-500 hover:text-brand-600">
          ← Voltar para itens
        </Link>
        <h1 className="page-title mt-1">Cadastrar item</h1>
      </div>

      <GerenciarCategorias categorias={(categorias ?? []) as Categoria[]} />

      <FormItem
        categorias={(categorias ?? []) as Categoria[]}
        fornecedores={(fornecedores ?? []) as Fornecedor[]}
        usuarioId={perfil.id}
      />
    </div>
  );
}
