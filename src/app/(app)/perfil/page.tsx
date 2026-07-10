import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { ROTULO_PAPEL } from "@/lib/tipos";
import FormPerfil from "@/components/FormPerfil";

export const dynamic = "force-dynamic";

export default async function PerfilPage() {
  const perfil = await obterPerfilOuRedirecionar();

  return (
    <div className="mx-auto max-w-2xl space-y-5">
      <div>
        <h1 className="page-title">Meu perfil</h1>
        <p className="text-slate-500">
          {perfil.email} · {ROTULO_PAPEL[perfil.papel]}
        </p>
      </div>
      <FormPerfil nomeAtual={perfil.nome} usuarioId={perfil.id} />
    </div>
  );
}
