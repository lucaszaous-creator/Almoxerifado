"use client";

import { useState } from "react";
import { montarConsulta, urlGoogle, urlGoogleShopping, urlMercadoLivre, type DadosProduto } from "@/lib/busca";

type Resultado = { titulo: string; link: string; fonte: string; snippet: string };

export default function PesquisaPrecos({
  produto,
  compacto = false,
}: {
  produto: DadosProduto;
  compacto?: boolean;
}) {
  const [b2b, setB2b] = useState(true);
  const [carregando, setCarregando] = useState(false);
  const [resultados, setResultados] = useState<Resultado[] | null>(null);
  const [semApi, setSemApi] = useState(false);
  const [erro, setErro] = useState<string | null>(null);

  const temNome = produto.nome.trim().length > 0;
  const consulta = montarConsulta(produto, b2b);

  async function buscarNoApp() {
    setCarregando(true);
    setErro(null);
    setSemApi(false);
    setResultados(null);
    try {
      const resp = await fetch(`/api/busca-precos?q=${encodeURIComponent(consulta)}`);
      const dados = await resp.json();
      if (dados.configurado === false) {
        setSemApi(true);
      } else if (dados.erro) {
        setErro(dados.erro);
      } else {
        setResultados(dados.resultados ?? []);
      }
    } catch {
      setErro("Não foi possível buscar agora.");
    } finally {
      setCarregando(false);
    }
  }

  const botoesLink = (
    <div className="flex flex-wrap gap-2">
      <a href={urlGoogle(consulta)} target="_blank" rel="noreferrer" className={temNome ? "btn-primary" : "btn-primary pointer-events-none opacity-50"}>
        🔎 Google
      </a>
      <a href={urlGoogleShopping(consulta)} target="_blank" rel="noreferrer" className="btn-secondary">
        🛍️ Shopping
      </a>
      <a href={urlMercadoLivre(consulta)} target="_blank" rel="noreferrer" className="btn-secondary">
        🟡 Mercado Livre
      </a>
    </div>
  );

  if (compacto) {
    return (
      <div className="space-y-2">
        <div className="flex flex-wrap items-center gap-3">
          {botoesLink}
          <label className="flex items-center gap-1.5 text-xs text-slate-600">
            <input type="checkbox" checked={b2b} onChange={(e) => setB2b(e.target.checked)} className="h-3.5 w-3.5 rounded border-slate-300" />
            foco CNPJ/atacado
          </label>
        </div>
        {temNome && <p className="text-xs text-slate-400">Busca: “{consulta}”</p>}
      </div>
    );
  }

  return (
    <div className="card p-6">
      <h2 className="mb-1 font-semibold text-slate-800">Pesquisar preços na web</h2>
      <p className="mb-4 text-sm text-slate-500">
        Pesquisa o produto no Google a partir do nome, marca e código de barras.
      </p>

      <div className="mb-3 flex flex-wrap items-center gap-3">
        {botoesLink}
        <label className="flex items-center gap-1.5 text-sm text-slate-600">
          <input type="checkbox" checked={b2b} onChange={(e) => setB2b(e.target.checked)} className="h-4 w-4 rounded border-slate-300" />
          foco CNPJ / atacado / nota fiscal
        </label>
      </div>

      {temNome && (
        <p className="mb-3 text-xs text-slate-400">
          Consulta: <span className="font-medium text-slate-500">{consulta}</span>
        </p>
      )}

      <button onClick={buscarNoApp} className="btn-secondary" disabled={!temNome || carregando}>
        {carregando ? "Buscando..." : "Buscar e mostrar aqui"}
      </button>

      {semApi && (
        <p className="mt-3 rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-700">
          Resultados dentro do app não estão configurados. Use os botões acima (abrem o Google), ou
          configure <code>GOOGLE_CSE_KEY</code> e <code>GOOGLE_CSE_CX</code> no ambiente para ver os
          resultados aqui.
        </p>
      )}
      {erro && <p className="mt-3 rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">{erro}</p>}

      {resultados && resultados.length === 0 && !erro && (
        <p className="mt-3 text-sm text-slate-400">Nenhum resultado.</p>
      )}
      {resultados && resultados.length > 0 && (
        <ul className="mt-4 space-y-3">
          {resultados.map((r, k) => (
            <li key={k} className="border-b border-slate-100 pb-3">
              <a href={r.link} target="_blank" rel="noreferrer" className="font-medium text-brand-600 hover:underline">
                {r.titulo}
              </a>
              <p className="text-xs text-emerald-700">{r.fonte}</p>
              <p className="text-sm text-slate-500">{r.snippet}</p>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
