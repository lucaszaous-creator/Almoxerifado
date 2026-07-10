"use client";

import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";

export default function BotaoSair() {
  const router = useRouter();
  const supabase = criarClienteBrowser();

  async function sair() {
    await supabase.auth.signOut();
    router.push("/login");
    router.refresh();
  }

  return (
    <button
      onClick={sair}
      className="rounded-lg px-3 py-2 text-sm font-medium text-slate-600 hover:bg-slate-100"
    >
      Sair
    </button>
  );
}
