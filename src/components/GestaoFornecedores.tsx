"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";
import { useConfirm } from "@/components/ui/Confirm";
import type { Fornecedor } from "@/lib/tipos";

const VAZIO = { nome: "", cnpj: "", contato: "", telefone: "", email: "", observacao: "", fornece_cnpj: true };

export default function GestaoFornecedores({ fornecedores }: { fornecedores: Fornecedor[] }) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const toast = useToast();
  const confirm = useConfirm();

  const [form, setForm] = useState({ ...VAZIO });
  const [editando, setEditando] = useState<string | null>(null);
  const [aberto, setAberto] = useState(false);
  const [salvando, setSalvando] = useState(false);

  function set<K extends keyof typeof form>(k: K, v: (typeof form)[K]) {
    setForm((f) => ({ ...f, [k]: v }));
  }

  function editar(f: Fornecedor) {
    setEditando(f.id);
    setForm({
      nome: f.nome,
      cnpj: f.cnpj ?? "",
      contato: f.contato ?? "",
      telefone: f.telefone ?? "",
      email: f.email ?? "",
      observacao: f.observacao ?? "",
      fornece_cnpj: f.fornece_cnpj ?? true,
    });
    setAberto(true);
  }

  function novo() {
    setEditando(null);
    setForm({ ...VAZIO });
    setAberto(true);
  }

  async function salvar(e: React.FormEvent) {
    e.preventDefault();
    if (!form.nome.trim()) return;
    setSalvando(true);
    const payload = {
      nome: form.nome.trim(),
      cnpj: form.cnpj.trim() || null,
      contato: form.contato.trim() || null,
      telefone: form.telefone.trim() || null,
      email: form.email.trim() || null,
      observacao: form.observacao.trim() || null,
      fornece_cnpj: form.fornece_cnpj,
    };
    const resposta = editando
      ? await supabase.from("fornecedores").update(payload).eq("id", editando)
      : await supabase.from("fornecedores").insert(payload);
    setSalvando(false);
    if (resposta.error) {
      toast.erro(
        resposta.error.message.includes("duplicate")
          ? "Já existe um fornecedor com esse nome."
          : resposta.error.message
      );
      return;
    }
    toast.sucesso(editando ? "Fornecedor atualizado." : "Fornecedor cadastrado.");
    setAberto(false);
    setForm({ ...VAZIO });
    setEditando(null);
    router.refresh();
  }

  async function excluir(f: Fornecedor) {
    const ok = await confirm({
      titulo: "Excluir fornecedor?",
      mensagem: `"${f.nome}" será removido. Itens vinculados ficam sem fornecedor.`,
      confirmar: "Excluir",
      perigo: true,
    });
    if (!ok) return;
    const { error } = await supabase.from("fornecedores").delete().eq("id", f.id);
    if (error) {
      toast.erro(error.message);
      return;
    }
    toast.sucesso("Fornecedor excluído.");
    router.refresh();
  }

  return (
    <div className="space-y-4">
      {!aberto && (
        <button onClick={novo} className="btn-primary">
          + Novo fornecedor
        </button>
      )}

      {aberto && (
        <form onSubmit={salvar} className="card space-y-4 p-6">
          <h2 className="font-semibold text-slate-800">
            {editando ? "Editar fornecedor" : "Novo fornecedor"}
          </h2>
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="sm:col-span-2">
              <label className="label">Nome / Razão social *</label>
              <input className="input" value={form.nome} onChange={(e) => set("nome", e.target.value)} required />
            </div>
            <div>
              <label className="label">CNPJ</label>
              <input className="input" value={form.cnpj} onChange={(e) => set("cnpj", e.target.value)} />
            </div>
            <div>
              <label className="label">Contato (pessoa)</label>
              <input className="input" value={form.contato} onChange={(e) => set("contato", e.target.value)} />
            </div>
            <div>
              <label className="label">Telefone</label>
              <input className="input" value={form.telefone} onChange={(e) => set("telefone", e.target.value)} />
            </div>
            <div>
              <label className="label">E-mail</label>
              <input className="input" type="email" value={form.email} onChange={(e) => set("email", e.target.value)} />
            </div>
            <div className="sm:col-span-2">
              <label className="label">Observação</label>
              <textarea className="input" rows={2} value={form.observacao} onChange={(e) => set("observacao", e.target.value)} />
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-700 sm:col-span-2">
              <input
                type="checkbox"
                checked={form.fornece_cnpj}
                onChange={(e) => set("fornece_cnpj", e.target.checked)}
                className="h-4 w-4 rounded border-slate-300"
              />
              Fornece para CNPJ (pessoa jurídica / B2B)
            </label>
          </div>
          <div className="flex gap-3">
            <button type="submit" className="btn-primary" disabled={salvando}>
              {salvando ? "Salvando..." : "Salvar"}
            </button>
            <button type="button" className="btn-secondary" onClick={() => setAberto(false)}>
              Cancelar
            </button>
          </div>
        </form>
      )}

      <div className="card overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead className="bg-slate-50">
              <tr>
                <th className="th">Fornecedor</th>
                <th className="th">Contato</th>
                <th className="th">Telefone</th>
                <th className="th"></th>
              </tr>
            </thead>
            <tbody>
              {fornecedores.map((f) => (
                <tr key={f.id} className="border-t border-slate-100">
                  <td className="px-4 py-3">
                    <p className="font-medium text-slate-800">
                      {f.nome}
                      {f.fornece_cnpj && <span className="badge ml-2 bg-brand-100 text-brand-700">CNPJ</span>}
                    </p>
                    {f.cnpj && <p className="text-xs text-slate-400">{f.cnpj}</p>}
                  </td>
                  <td className="px-4 py-3 text-slate-600">
                    {f.contato || "—"}
                    {f.email && <p className="text-xs text-slate-400">{f.email}</p>}
                  </td>
                  <td className="px-4 py-3 text-slate-600">{f.telefone || "—"}</td>
                  <td className="px-4 py-3 text-right">
                    <button onClick={() => editar(f)} className="mr-2 text-brand-600 hover:underline">
                      Editar
                    </button>
                    <button onClick={() => excluir(f)} className="text-rose-600 hover:underline">
                      Excluir
                    </button>
                  </td>
                </tr>
              ))}
              {fornecedores.length === 0 && (
                <tr>
                  <td colSpan={4} className="px-4 py-10 text-center text-slate-400">
                    Nenhum fornecedor cadastrado.
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
