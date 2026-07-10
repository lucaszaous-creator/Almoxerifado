import { redirect } from "next/navigation";
import { criarClienteServidor } from "@/lib/supabase/server";
import { obterPerfilOuRedirecionar } from "@/lib/sessao";
import { fmtDataHora } from "@/lib/format";
import type { Auditoria } from "@/lib/tipos";

export const dynamic = "force-dynamic";

const COR: Record<string, string> = {
  INSERT: "bg-emerald-100 text-emerald-700",
  UPDATE: "bg-blue-100 text-blue-700",
  DELETE: "bg-rose-100 text-rose-700",
};

export default async function AuditoriaPage() {
  const perfil = await obterPerfilOuRedirecionar();
  if (perfil.papel !== "gerente") redirect("/");

  const supabase = criarClienteServidor();
  const { data } = await supabase
    .from("auditoria")
    .select("*, profiles(nome)")
    .order("criado_em", { ascending: false })
    .limit(300);
  const logs = (data ?? []) as Auditoria[];

  return (
    <div className="mx-auto max-w-4xl space-y-5">
      <div>
        <h1 className="page-title">Auditoria</h1>
        <p className="text-slate-500">Registro de quem fez o quê no sistema.</p>
      </div>

      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="th">Data</th>
                <th className="th">Usuário</th>
                <th className="th">Ação</th>
                <th className="th">Descrição</th>
              </tr>
            </thead>
            <tbody>
              {logs.map((l) => (
                <tr key={l.id} className="border-t border-slate-100">
                  <td className="whitespace-nowrap px-4 py-2.5 text-slate-500">{fmtDataHora(l.criado_em)}</td>
                  <td className="px-4 py-2.5 text-slate-700">{l.profiles?.nome ?? "sistema"}</td>
                  <td className="px-4 py-2.5">
                    <span className={`badge ${COR[l.acao] ?? "bg-slate-100 text-slate-600"}`}>{l.acao}</span>
                  </td>
                  <td className="px-4 py-2.5 text-slate-600">{l.descricao ?? l.entidade}</td>
                </tr>
              ))}
              {logs.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-4 py-8 text-center text-slate-400">
                    Nenhum registro ainda.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
