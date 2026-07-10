import Link from "next/link";
import { redirect, notFound } from "next/navigation";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { podeGerenciar, type Categoria, type Fornecedor, type Item } from "@/lib/tipos";
import FormItem from "@/components/FormItem";

export const dynamic = "force-dynamic";

export default async function EditarItemPage({ params }: { params: { id: string } }) {
  const perfil = await obterPerfilOuRedirecionar();
  if (!podeGerenciar(perfil.papel)) redirect("/itens");

  const supabase = criarClienteServidor();
  const { data: item } = await supabase.from("itens").select("*").eq("id", params.id).single();
  if (!item) notFound();

  const { data: categorias } = await supabase.from("categorias").select("*").order("nome");
  const { data: fornecedores } = await supabase.from("fornecedores").select("*").order("nome");

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <Link href={`/itens/${params.id}`} className="text-sm text-slate-500 hover:text-brand-600">
          ← Voltar
        </Link>
        <h1 className="page-title mt-1">Editar item</h1>
        <p className="text-sm text-slate-400">
          Para alterar a quantidade, use &quot;Movimentar estoque&quot; na página do item.
        </p>
      </div>

      <FormItem
        categorias={(categorias ?? []) as Categoria[]}
        fornecedores={(fornecedores ?? []) as Fornecedor[]}
        item={item as Item}
        usuarioId={perfil.id}
      />
    </div>
  );
}
