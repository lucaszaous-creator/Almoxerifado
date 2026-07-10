"use client";

import { useEffect, useRef, useState } from "react";

/* Controles do scanner ZXing (evita depender do tipo aqui). */
type Controls = { stop: () => void };

export default function LeitorCodigoBarras({ onDetectar }: { onDetectar: (codigo: string) => void }) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const controlsRef = useRef<Controls | null>(null);
  const [ativo, setAtivo] = useState(false);
  const [carregando, setCarregando] = useState(false);
  const [erro, setErro] = useState<string | null>(null);
  const [manual, setManual] = useState("");

  useEffect(() => {
    return () => parar();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function parar() {
    controlsRef.current?.stop();
    controlsRef.current = null;
    setAtivo(false);
  }

  async function iniciar() {
    setErro(null);
    setCarregando(true);
    try {
      const { BrowserMultiFormatReader } = await import("@zxing/browser");
      const reader = new BrowserMultiFormatReader();
      const controls = await reader.decodeFromConstraints(
        { video: { facingMode: { ideal: "environment" } }, audio: false },
        videoRef.current!,
        (result) => {
          if (result) {
            const texto = result.getText();
            parar();
            onDetectar(texto);
          }
        }
      );
      controlsRef.current = controls as unknown as Controls;
      setAtivo(true);
    } catch (e: unknown) {
      const nome = (e as { name?: string })?.name;
      setErro(
        nome === "NotAllowedError"
          ? "Permissão de câmera negada. Autorize o acesso à câmera nas configurações do navegador."
          : "Não foi possível abrir a câmera neste dispositivo."
      );
    } finally {
      setCarregando(false);
    }
  }

  return (
    <div className="card p-4">
      <div className="relative aspect-video w-full overflow-hidden rounded-lg bg-black">
        <video ref={videoRef} className="h-full w-full object-cover" muted playsInline />
        {ativo && (
          <div className="pointer-events-none absolute inset-0 flex items-center justify-center">
            <div className="h-1/3 w-3/4 rounded-lg border-2 border-white/80 shadow-[0_0_0_9999px_rgba(0,0,0,0.35)]" />
          </div>
        )}
        {!ativo && (
          <div className="absolute inset-0 flex flex-col items-center justify-center gap-3 text-white">
            <span className="text-4xl">📷</span>
            <button onClick={iniciar} className="btn-primary" disabled={carregando}>
              {carregando ? "Abrindo câmera..." : "Ativar câmera"}
            </button>
          </div>
        )}
      </div>

      {ativo && (
        <div className="mt-3 flex items-center justify-between">
          <p className="text-sm text-slate-500">Aponte para o código de barras...</p>
          <button onClick={parar} className="btn-secondary py-1">
            Parar
          </button>
        </div>
      )}

      {erro && <p className="mt-3 rounded-lg bg-rose-50 px-3 py-2 text-sm text-rose-700">{erro}</p>}

      <form
        onSubmit={(e) => {
          e.preventDefault();
          if (manual.trim()) onDetectar(manual.trim());
        }}
        className="mt-3 flex gap-2"
      >
        <input
          className="input"
          placeholder="Ou digite o código de barras / SKU"
          value={manual}
          onChange={(e) => setManual(e.target.value)}
        />
        <button type="submit" className="btn-secondary whitespace-nowrap">
          Buscar
        </button>
      </form>
    </div>
  );
}
