"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";

export default function LoginPage() {
  const router = useRouter();
  const supabase = criarClienteBrowser();

  const [modo, setModo] = useState<"entrar" | "cadastrar">("entrar");
  const [nome, setNome] = useState("");
  const [email, setEmail] = useState("");
  const [senha, setSenha] = useState("");
  const [erro, setErro] = useState<string | null>(null);
  const [aviso, setAviso] = useState<string | null>(null);
  const [carregando, setCarregando] = useState(false);

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    setErro(null);
    setAviso(null);
    setCarregando(true);

    try {
      if (modo === "entrar") {
        const { error } = await supabase.auth.signInWithPassword({ email, password: senha });
        if (error) throw error;
        router.push("/");
        router.refresh();
      } else {
        const { data, error } = await supabase.auth.signUp({
          email,
          password: senha,
          options: { data: { nome } },
        });
        if (error) throw error;
        if (data.session) {
          router.push("/");
          router.refresh();
        } else {
          setAviso("Conta criada! Confirme seu e-mail (se exigido) e depois entre.");
          setModo("entrar");
        }
      }
    } catch (err: unknown) {
      setErro(traduzirErro(err));
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div className="flex min-h-screen items-center justify-center bg-gradient-to-br from-brand-600 to-brand-800 p-4">
      <div className="w-full max-w-md">
        <div className="mb-6 text-center text-white">
          <div className="mx-auto mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-white/15 text-3xl">
            📦
          </div>
          <h1 className="text-2xl font-bold">Almoxarifado &amp; Compras</h1>
          <p className="text-brand-100">Controle de estoque e pedidos</p>
        </div>

        <form onSubmit={enviar} className="card space-y-4 p-6">
          <div className="flex rounded-lg bg-slate-100 p-1 text-sm font-medium">
            <button
              type="button"
              onClick={() => setModo("entrar")}
              className={`flex-1 rounded-md py-1.5 ${modo === "entrar" ? "bg-white shadow" : "text-slate-500"}`}
            >
              Entrar
            </button>
            <button
              type="button"
              onClick={() => setModo("cadastrar")}
              className={`flex-1 rounded-md py-1.5 ${modo === "cadastrar" ? "bg-white shadow" : "text-slate-500"}`}
            >
              Criar conta
            </button>
          </div>

          {modo === "cadastrar" && (
            <div>
              <label className="label">Nome completo</label>
              <input className="input" value={nome} onChange={(e) => setNome(e.target.value)} required />
            </div>
          )}

          <div>
            <label className="label">E-mail</label>
            <input
              className="input"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              required
            />
          </div>

          <div>
            <label className="label">Senha</label>
            <input
              className="input"
              type="password"
              value={senha}
              onChange={(e) => setSenha(e.target.value)}
              minLength={6}
              required
            />
          </div>

          {erro && <p className="rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">{erro}</p>}
          {aviso && <p className="rounded-lg bg-emerald-50 px-3 py-2 text-sm text-emerald-700">{aviso}</p>}

          <button type="submit" className="btn-primary w-full" disabled={carregando}>
            {carregando ? "Aguarde..." : modo === "entrar" ? "Entrar" : "Criar conta"}
          </button>

          {modo === "cadastrar" && (
            <p className="text-center text-xs text-slate-400">
              O primeiro usuário cadastrado torna-se Gerente automaticamente.
            </p>
          )}
        </form>
      </div>
    </div>
  );
}

function traduzirErro(err: unknown): string {
  const msg = err instanceof Error ? err.message : String(err);
  if (msg.includes("Invalid login credentials")) return "E-mail ou senha inválidos.";
  if (msg.includes("already registered")) return "Este e-mail já está cadastrado.";
  if (msg.includes("Password should be")) return "A senha deve ter ao menos 6 caracteres.";
  return msg;
}
