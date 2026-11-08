"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";

export default function FormPerfil({ nomeAtual, usuarioId }: { nomeAtual: string; usuarioId: string }) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const toast = useToast();
  const [nome, setNome] = useState(nomeAtual);
  const [senha, setSenha] = useState("");
  const [senha2, setSenha2] = useState("");
  const [salvando, setSalvando] = useState(false);

  async function salvarNome(e: React.FormEvent) {
    e.preventDefault();
    setSalvando(true);
    const { error } = await supabase.from("profiles").update({ nome: nome.trim() }).eq("id", usuarioId);
    setSalvando(false);
    if (error) return toast.erro(error.message);
    toast.sucesso("Nome atualizado.");
    router.refresh();
  }

  async function trocarSenha(e: React.FormEvent) {
    e.preventDefault();
    if (senha.length < 6) return toast.erro("A senha deve ter ao menos 6 caracteres.");
    if (senha !== senha2) return toast.erro("As senhas não conferem.");
    setSalvando(true);
    const { error } = await supabase.auth.updateUser({ password: senha });
    setSalvando(false);
    if (error) return toast.erro(error.message);
    setSenha("");
    setSenha2("");
    toast.sucesso("Senha alterada.");
  }

  return (
    <div className="space-y-5">
      <form onSubmit={salvarNome} className="card space-y-4 p-6">
        <h2 className="font-semibold text-slate-800">Dados</h2>
        <div>
          <label className="label">Nome</label>
          <input className="input" value={nome} onChange={(e) => setNome(e.target.value)} required />
        </div>
        <button type="submit" className="btn-primary" disabled={salvando}>
          Salvar nome
        </button>
      </form>

      <form onSubmit={trocarSenha} className="card space-y-4 p-6">
        <h2 className="font-semibold text-slate-800">Trocar senha</h2>
        <div className="grid gap-4 sm:grid-cols-2">
          <div>
            <label className="label">Nova senha</label>
            <input className="input" type="password" value={senha} onChange={(e) => setSenha(e.target.value)} minLength={6} />
          </div>
          <div>
            <label className="label">Confirmar senha</label>
            <input className="input" type="password" value={senha2} onChange={(e) => setSenha2(e.target.value)} minLength={6} />
          </div>
        </div>
        <button type="submit" className="btn-secondary" disabled={salvando}>
          Alterar senha
        </button>
      </form>
    </div>
  );
}
