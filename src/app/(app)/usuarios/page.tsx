import { redirect } from "next/navigation";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import type { Profile } from "@/lib/tipos";
import TabelaUsuarios from "@/components/TabelaUsuarios";

export const dynamic = "force-dynamic";

export default async function UsuariosPage() {
  const perfil = await obterPerfilOuRedirecionar();
  if (perfil.papel !== "gerente") redirect("/");

  const supabase = criarClienteServidor();
  const { data } = await supabase.from("profiles").select("*").order("nome");

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <div>
        <h1 className="page-title">Usuários</h1>
        <p className="text-slate-500">
          Defina quem é gerente, almoxarife (responsável) ou funcionário.
        </p>
      </div>
      <TabelaUsuarios usuarios={(data ?? []) as Profile[]} meuId={perfil.id} />
    </div>
  );
}
