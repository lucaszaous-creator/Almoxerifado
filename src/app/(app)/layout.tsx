import Link from "next/link";
import Sidebar from "@/components/Sidebar";
import SinoNotificacoes from "@/components/SinoNotificacoes";
import BotaoSair from "@/components/BotaoSair";
import MenuMobile from "@/components/MenuMobile";
import BuscaGlobal from "@/components/BuscaGlobal";
import ThemeToggle from "@/components/ThemeToggle";
import Providers from "@/components/ui/Providers";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";

export default async function AppLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const perfil = await obterPerfilOuRedirecionar();

  return (
    <Providers>
      <div className="flex min-h-screen">
        <Sidebar nome={perfil.nome} papel={perfil.papel} />

        <div className="flex min-w-0 flex-1 flex-col">
          <header className="sticky top-0 z-40 flex items-center gap-2 border-b border-slate-200 bg-white/95 px-3 py-3 backdrop-blur sm:gap-3 sm:px-4 md:px-6">
            <MenuMobile nome={perfil.nome} papel={perfil.papel} />
            <div className="min-w-0 flex-1">
              <BuscaGlobal />
            </div>
            <div className="flex shrink-0 items-center gap-0.5 sm:gap-1">
              <ThemeToggle />
              <SinoNotificacoes usuarioId={perfil.id} />
              <Link
                href="/perfil"
                className="hidden rounded-full p-2 text-slate-600 hover:bg-slate-100 sm:block"
                title="Meu perfil"
              >
                👤
              </Link>
              <BotaoSair />
            </div>
          </header>

          <main className="flex-1 p-4 md:p-6">{children}</main>
        </div>
      </div>
    </Providers>
  );
}
