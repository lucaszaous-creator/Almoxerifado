import { redirect } from "next/navigation";
import Link from "next/link";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { podeGerenciar } from "@/lib/tipos";
import EtiquetasImpressao from "@/components/EtiquetasImpressao";

export const dynamic = "force-dynamic";

export default async function EtiquetasPage() {
  const perfil = await obterPerfilOuRedirecionar();
  if (!podeGerenciar(perfil.papel)) redirect("/itens");

  const supabase = criarClienteServidor();
  const { data } = await supabase
    .from("itens")
    .select("id, nome, codigo, codigo_barras, localizacao")
    .eq("ativo", true)
    .order("nome");

  return (
    <div className="mx-auto max-w-5xl space-y-5">
      <div className="no-print">
        <Link href="/itens" className="text-sm text-slate-500 hover:text-brand-600">
          ← Voltar para itens
        </Link>
        <h1 className="page-title mt-1">Etiquetas de código de barras</h1>
        <p className="text-slate-500">
          Gere e imprima etiquetas (Code 128) com nome, código de barras e localização.
        </p>
      </div>
      <EtiquetasImpressao itens={data ?? []} />
    </div>
  );
}
