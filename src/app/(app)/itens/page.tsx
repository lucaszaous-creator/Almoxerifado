import Link from "next/link";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { podeGerenciar, type Item, type Categoria, type Fornecedor } from "@/lib/tipos";
import ItensLista from "@/components/ItensLista";

export const dynamic = "force-dynamic";

export default async function ItensPage({
  searchParams,
}: {
  searchParams: { filtro?: string };
}) {
  const perfil = await obterPerfilOuRedirecionar();
  const supabase = criarClienteServidor();

  const [itensRes, categoriasRes, fornecedoresRes] = await Promise.all([
    supabase.from("itens").select("*, categorias(id, nome), fornecedores(nome)").order("nome"),
    supabase.from("categorias").select("*").order("nome"),
    supabase.from("fornecedores").select("*").order("nome"),
  ]);
  const itens = itensRes.data;
  const categorias = categoriasRes.data;
  const fornecedores = fornecedoresRes.data;

  return (
    <div className="mx-auto max-w-6xl space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h1 className="page-title">Consultar itens</h1>
          <p className="text-slate-500">
            Verifique se um item já existe, quantidade e localização antes de ir ao almoxarifado.
          </p>
        </div>
        {podeGerenciar(perfil.papel) && (
          <div className="flex flex-wrap gap-2">
            <Link href="/itens/etiquetas" className="btn-secondary">
              🏷️ Etiquetas
            </Link>
            <Link href="/itens/importar" className="btn-secondary">
              ⬆️ Importar CSV
            </Link>
            <Link href="/itens/novo" className="btn-primary">
              + Cadastrar item
            </Link>
          </div>
        )}
      </div>

      <ItensLista
        itens={(itens ?? []) as Item[]}
        categorias={(categorias ?? []) as Categoria[]}
        fornecedores={(fornecedores ?? []) as Fornecedor[]}
        filtroInicial={searchParams.filtro ?? ""}
        podeGerenciar={podeGerenciar(perfil.papel)}
      />
    </div>
  );
}
