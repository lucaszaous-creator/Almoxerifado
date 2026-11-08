"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";
import type { Configuracao } from "@/lib/tipos";

export default function FormConfiguracoes({ config }: { config: Configuracao }) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const toast = useToast();
  const [empresa, setEmpresa] = useState(config?.empresa_nome ?? "");
  const [moeda, setMoeda] = useState(config?.moeda ?? "BRL");
  const [dias, setDias] = useState<number>(config?.dias_alerta_validade ?? 30);
  const [salvando, setSalvando] = useState(false);

  async function salvar(e: React.FormEvent) {
    e.preventDefault();
    setSalvando(true);
    const { error } = await supabase
      .from("configuracoes")
      .update({
        empresa_nome: empresa.trim() || "Minha Empresa",
        moeda,
        dias_alerta_validade: Math.max(1, dias),
        atualizado_em: new Date().toISOString(),
      })
      .eq("id", true);
    setSalvando(false);
    if (error) return toast.erro(error.message);
    toast.sucesso("Configurações salvas.");
    router.refresh();
  }

  return (
    <form onSubmit={salvar} className="card space-y-4 p-6">
      <div>
        <label className="label">Nome da empresa</label>
        <input className="input" value={empresa} onChange={(e) => setEmpresa(e.target.value)} />
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <div>
          <label className="label">Moeda</label>
          <select className="input" value={moeda} onChange={(e) => setMoeda(e.target.value)}>
            <option value="BRL">Real (R$)</option>
            <option value="USD">Dólar (US$)</option>
            <option value="EUR">Euro (€)</option>
          </select>
        </div>
        <div>
          <label className="label">Alerta de validade (dias)</label>
          <input
            className="input"
            type="number"
            min={1}
            value={dias}
            onChange={(e) => setDias(Number(e.target.value))}
          />
          <p className="mt-1 text-xs text-slate-400">
            Itens que vencem dentro desse prazo geram alerta.
          </p>
        </div>
      </div>
      <button type="submit" className="btn-primary" disabled={salvando}>
        {salvando ? "Salvando..." : "Salvar configurações"}
      </button>
    </form>
  );
}
