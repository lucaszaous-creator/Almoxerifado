import { redirect } from "next/navigation";
import Link from "next/link";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { podeGerenciar, type Categoria } from "@/lib/tipos";
import ImportarItens from "@/components/ImportarItens";

export const dynamic = "force-dynamic";

export default async function ImportarPage() {
  const perfil = await obterPerfilOuRedirecionar();
  if (!podeGerenciar(perfil.papel)) redirect("/itens");

  const supabase = criarClienteServidor();
  const { data: categorias } = await supabase.from("categorias").select("*").order("nome");

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <Link href="/itens" className="text-sm text-slate-500 hover:text-brand-600">
          ← Voltar para itens
        </Link>
        <h1 className="page-title mt-1">Importar itens (CSV)</h1>
        <p className="text-slate-500">
          Cadastre vários itens de uma vez a partir de uma planilha CSV.
        </p>
      </div>
      <ImportarItens categorias={(categorias ?? []) as Categoria[]} usuarioId={perfil.id} />
    </div>
  );
}
