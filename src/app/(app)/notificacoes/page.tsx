import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { criarClienteServidor } from "@/lib/supabase/server";
import PainelNotificacoes from "@/components/PainelNotificacoes";
import type { Notificacao } from "@/lib/tipos";

export const dynamic = "force-dynamic";

export default async function NotificacoesPage() {
  await obterPerfilOuRedirecionar();
  const supabase = criarClienteServidor();
  const { data } = await supabase
    .from("notificacoes")
    .select("*")
    .order("criado_em", { ascending: false })
    .limit(100);

  return (
    <div className="mx-auto max-w-3xl space-y-5">
      <div>
        <h1 className="page-title">Notificações</h1>
        <p className="text-slate-500">Alertas de estoque, validade e pedidos de compra.</p>
      </div>
      <PainelNotificacoes iniciais={(data ?? []) as Notificacao[]} />
    </div>
  );
}
