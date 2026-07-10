import { redirect } from "next/navigation";
import { criarClienteServidor } from "@/lib/supabase/server";
import type { Profile } from "@/lib/tipos";

/**
 * Retorna o profile do usuário logado (server-side).
 * Redireciona para /login se não houver sessão.
 */
export async function obterPerfilOuRedirecionar(): Promise<Profile> {
  const supabase = criarClienteServidor();
  const {
    data: { user },
  } = await supabase.auth.getUser();

  if (!user) redirect("/login");

  const { data: perfil } = await supabase
    .from("profiles")
    .select("*")
    .eq("id", user.id)
    .single();

  if (!perfil) redirect("/login");
  return perfil as Profile;
}
