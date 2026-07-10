import Link from "next/link";

export default function NotFound() {
  return (
    <div className="flex min-h-screen flex-col items-center justify-center gap-4 bg-slate-100 p-6 text-center">
      <span className="text-5xl">🧭</span>
      <h1 className="page-title">Página não encontrada</h1>
      <p className="max-w-sm text-slate-500">
        O endereço acessado não existe ou o item foi removido.
      </p>
      <Link href="/" className="btn-primary">
        Voltar ao início
      </Link>
    </div>
  );
}
