"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { ROTULO_PAPEL, type PapelUsuario, type Profile } from "@/lib/tipos";

const PAPEIS: PapelUsuario[] = ["gerente", "almoxarife", "funcionario"];

export default function TabelaUsuarios({
  usuarios,
  meuId,
}: {
  usuarios: Profile[];
  meuId: string;
}) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const [lista, setLista] = useState(usuarios);
  const [erro, setErro] = useState<string | null>(null);

  async function mudarPapel(id: string, papel: PapelUsuario) {
    setErro(null);
    const { error } = await supabase.from("profiles").update({ papel }).eq("id", id);
    if (error) {
      setErro(error.message);
      return;
    }
    setLista((p) => p.map((u) => (u.id === id ? { ...u, papel } : u)));
    router.refresh();
  }

  async function alternarAtivo(id: string, ativo: boolean) {
    const { error } = await supabase.from("profiles").update({ ativo }).eq("id", id);
    if (error) {
      setErro(error.message);
      return;
    }
    setLista((p) => p.map((u) => (u.id === id ? { ...u, ativo } : u)));
  }

  return (
    <div className="card overflow-hidden">
      {erro && <p className="bg-rose-50 px-4 py-2 text-sm text-rose-700">{erro}</p>}
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs uppercase text-slate-500">
            <tr>
              <th className="px-4 py-3 font-medium">Nome</th>
              <th className="px-4 py-3 font-medium">E-mail</th>
              <th className="px-4 py-3 font-medium">Papel</th>
              <th className="px-4 py-3 font-medium">Ativo</th>
            </tr>
          </thead>
          <tbody>
            {lista.map((u) => (
              <tr key={u.id} className="border-t border-slate-100">
                <td className="px-4 py-3 font-medium text-slate-800">
                  {u.nome}
                  {u.id === meuId && <span className="ml-1 text-xs text-slate-400">(você)</span>}
                </td>
                <td className="px-4 py-3 text-slate-600">{u.email}</td>
                <td className="px-4 py-3">
                  <select
                    className="input py-1"
                    value={u.papel}
                    disabled={u.id === meuId}
                    onChange={(e) => mudarPapel(u.id, e.target.value as PapelUsuario)}
                  >
                    {PAPEIS.map((p) => (
                      <option key={p} value={p}>
                        {ROTULO_PAPEL[p]}
                      </option>
                    ))}
                  </select>
                </td>
                <td className="px-4 py-3">
                  <button
                    onClick={() => alternarAtivo(u.id, !u.ativo)}
                    disabled={u.id === meuId}
                    className={`badge ${u.ativo ? "bg-emerald-100 text-emerald-700" : "bg-slate-200 text-slate-500"} disabled:opacity-60`}
                  >
                    {u.ativo ? "Ativo" : "Inativo"}
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="border-t border-slate-100 px-4 py-3 text-xs text-slate-400">
        Você não pode alterar o próprio papel para evitar perder o acesso de gerente.
      </p>
    </div>
  );
}
