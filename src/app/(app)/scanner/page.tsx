"use client";

import { useCallback, useEffect, useState } from "react";
import Link from "next/link";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";
import LeitorCodigoBarras from "@/components/LeitorCodigoBarras";
import PesquisaPrecos from "@/components/PesquisaPrecos";
import { fmtNumero } from "@/lib/format";
import { sanitizarCodigo } from "@/lib/db";
import { MOTIVOS_ENTRADA, MOTIVOS_SAIDA, podeGerenciar, type PapelUsuario } from "@/lib/tipos";

type ItemScan = {
  id: string;
  nome: string;
  codigo: string;
  unidade: string;
  quantidade: number;
  controla_lote: boolean;
};

export default function ScannerPage() {
  const supabase = criarClienteBrowser();
  const toast = useToast();
  const [papel, setPapel] = useState<PapelUsuario | null>(null);
  const [usuarioId, setUsuarioId] = useState<string>("");
  const [buscando, setBuscando] = useState(false);
  const [item, setItem] = useState<ItemScan | null>(null);
  const [naoEncontrado, setNaoEncontrado] = useState<string | null>(null);

  const [tipo, setTipo] = useState<"entrada" | "saida">("saida");
  const [qtd, setQtd] = useState<number>(1);
  const [motivo, setMotivo] = useState("");
  const [salvando, setSalvando] = useState(false);

  useEffect(() => {
    (async () => {
      const { data } = await supabase.auth.getUser();
      if (data.user) {
        setUsuarioId(data.user.id);
        const { data: perfil } = await supabase.from("profiles").select("papel").eq("id", data.user.id).single();
        setPapel((perfil?.papel as PapelUsuario) ?? "funcionario");
      }
    })();
  }, [supabase]);

  const gestor = podeGerenciar(papel);

  const carregarItem = useCallback(
    async (id: string) => {
      const { data } = await supabase
        .from("itens")
        .select("id, nome, codigo, unidade, quantidade, controla_lote")
        .eq("id", id)
        .single();
      if (data) setItem(data as ItemScan);
    },
    [supabase]
  );

  async function aoDetectar(codigoBruto: string) {
    const codigo = sanitizarCodigo(codigoBruto);
    if (!codigo) return;
    setBuscando(true);
    setNaoEncontrado(null);
    setItem(null);
    const { data, error } = await supabase
      .from("itens")
      .select("id, nome, codigo, unidade, quantidade, controla_lote")
      .or(`codigo_barras.eq.${codigo},codigo.eq.${codigo}`)
      .limit(1);
    setBuscando(false);
    if (error) return toast.erro(error.message);
    if (data && data.length > 0) {
      setItem(data[0] as ItemScan);
      setQtd(1);
      setMotivo("");
      toast.sucesso(`Item: ${data[0].nome}`);
    } else {
      setNaoEncontrado(codigo);
    }
  }

  async function registrar() {
    if (!item) return;
    if (qtd <= 0) return toast.erro("Quantidade inválida.");
    setSalvando(true);
    const { error } = await supabase.from("movimentacoes").insert({
      item_id: item.id,
      tipo,
      quantidade: qtd,
      motivo: motivo || (tipo === "entrada" ? "Compra" : "Consumo"),
      usuario_id: usuarioId,
      observacao: "Via scanner",
    });
    setSalvando(false);
    if (error) {
      return toast.erro(error.message.includes("insuficiente") ? "Estoque insuficiente para a saída." : error.message);
    }
    toast.sucesso(`${tipo === "entrada" ? "Entrada" : "Saída"} registrada!`);
    await carregarItem(item.id);
    setQtd(1);
  }

  const motivos = tipo === "entrada" ? MOTIVOS_ENTRADA : MOTIVOS_SAIDA;

  return (
    <div className="mx-auto max-w-lg space-y-5">
      <div>
        <h1 className="page-title">Escanear código de barras</h1>
        <p className="text-slate-500">
          Leia o código para localizar o item{gestor ? " e dar entrada/saída na hora" : ""}.
        </p>
      </div>

      <LeitorCodigoBarras onDetectar={aoDetectar} />

      {buscando && <p className="text-center text-sm text-slate-500">Buscando item...</p>}

      {item && (
        <div className="card space-y-4 p-5">
          <div className="flex items-center justify-between">
            <div>
              <p className="text-lg font-semibold text-slate-800">{item.nome}</p>
              <p className="text-xs text-slate-400">cód. {item.codigo}</p>
            </div>
            <div className="text-right">
              <p className="page-title">{fmtNumero(item.quantidade)}</p>
              <p className="text-xs text-slate-400">{item.unidade} em estoque</p>
            </div>
          </div>

          {gestor && item.controla_lote && (
            <p className="rounded-lg bg-amber-50 px-3 py-2 text-sm text-amber-700">
              Item controlado por lote — a movimentação por lote (FEFO) é feita na ficha do item.
            </p>
          )}

          {gestor && !item.controla_lote && (
            <div className="space-y-3">
              <div className="flex gap-2">
                <button
                  type="button"
                  onClick={() => setTipo("entrada")}
                  className={`flex-1 rounded-lg py-2 text-sm font-medium ${tipo === "entrada" ? "bg-emerald-600 text-white" : "bg-slate-100 text-slate-600"}`}
                >
                  Entrada
                </button>
                <button
                  type="button"
                  onClick={() => setTipo("saida")}
                  className={`flex-1 rounded-lg py-2 text-sm font-medium ${tipo === "saida" ? "bg-rose-600 text-white" : "bg-slate-100 text-slate-600"}`}
                >
                  Saída
                </button>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <input
                  className="input"
                  type="number"
                  min={0}
                  step="any"
                  value={qtd}
                  onChange={(e) => setQtd(Number(e.target.value))}
                  placeholder="Quantidade"
                />
                <select className="input" value={motivo} onChange={(e) => setMotivo(e.target.value)}>
                  <option value="">Motivo...</option>
                  {motivos.map((m) => (
                    <option key={m} value={m}>
                      {m}
                    </option>
                  ))}
                </select>
              </div>
              <button className="btn-primary w-full" onClick={registrar} disabled={salvando}>
                {salvando ? "Registrando..." : `Confirmar ${tipo}`}
              </button>
            </div>
          )}

          <div className="flex gap-2">
            <Link href={`/itens/${item.id}`} className="btn-secondary flex-1 text-center">
              Abrir ficha completa
            </Link>
            <button className="btn-secondary flex-1" onClick={() => setItem(null)}>
              Escanear outro
            </button>
          </div>
        </div>
      )}

      {naoEncontrado && (
        <div className="card space-y-3 p-4">
          <p className="text-sm text-slate-700">
            Nenhum item com o código <span className="font-mono font-medium">{naoEncontrado}</span>.
          </p>
          <div className="flex flex-wrap gap-2">
            {gestor && (
              <Link href="/itens/novo" className="btn-primary">
                Cadastrar novo item
              </Link>
            )}
            <button className="btn-secondary" onClick={() => setNaoEncontrado(null)}>
              Escanear outro
            </button>
          </div>
          <PesquisaPrecos compacto produto={{ nome: naoEncontrado, codigo_barras: naoEncontrado }} />
        </div>
      )}
    </div>
  );
}
