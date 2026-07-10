import { redirect } from "next/navigation";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import type { Configuracao } from "@/lib/tipos";
import FormConfiguracoes from "@/components/FormConfiguracoes";

export const dynamic = "force-dynamic";

export default async function ConfiguracoesPage() {
  const perfil = await obterPerfilOuRedirecionar();
  if (perfil.papel !== "gerente") redirect("/");

  const supabase = criarClienteServidor();
  const { data } = await supabase.from("configuracoes").select("*").eq("id", true).single();

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <div>
        <h1 className="page-title">Configurações</h1>
        <p className="text-slate-500">Parâmetros gerais do sistema.</p>
      </div>
      <FormConfiguracoes config={data as Configuracao} />
    </div>
  );
}
