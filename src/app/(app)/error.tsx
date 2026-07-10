"use client";

import Link from "next/link";
import { useEffect } from "react";

export default function Error({
  error,
  reset,
}: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <div className="mx-auto flex max-w-md flex-col items-center gap-4 py-16 text-center">
      <span className="text-5xl">⚠️</span>
      <h1 className="text-xl font-bold text-slate-800">Algo deu errado</h1>
      <p className="text-slate-500">
        Não foi possível carregar esta tela. Tente novamente; se persistir, verifique sua conexão.
      </p>
      <div className="flex gap-2">
        <button onClick={() => reset()} className="btn-primary">
          Tentar de novo
        </button>
        <Link href="/" className="btn-secondary">
          Ir para o início
        </Link>
      </div>
    </div>
  );
}
