import { NextResponse } from "next/server";

export const dynamic = "force-dynamic";

/**
 * Busca de preços via Google Custom Search JSON API (opcional).
 * Configure GOOGLE_CSE_KEY e GOOGLE_CSE_CX no ambiente para habilitar os
 * resultados dentro do app. Sem isso, a rota responde { configurado: false }
 * e o front usa os botões que abrem o Google em nova aba.
 */
export async function GET(request: Request) {
  const { searchParams } = new URL(request.url);
  const q = (searchParams.get("q") ?? "").trim();
  if (!q) return NextResponse.json({ configurado: true, resultados: [] });

  const key = process.env.GOOGLE_CSE_KEY;
  const cx = process.env.GOOGLE_CSE_CX;
  if (!key || !cx) {
    return NextResponse.json({ configurado: false });
  }

  try {
    const url =
      `https://www.googleapis.com/customsearch/v1?key=${encodeURIComponent(key)}` +
      `&cx=${encodeURIComponent(cx)}&num=8&gl=br&hl=pt-BR&q=${encodeURIComponent(q)}`;
    const resp = await fetch(url, { cache: "no-store" });
    if (!resp.ok) {
      return NextResponse.json({ configurado: true, erro: `Google respondeu ${resp.status}` }, { status: 200 });
    }
    const dados = await resp.json();
    const resultados = (dados.items ?? []).map((it: Record<string, unknown>) => ({
      titulo: it.title as string,
      link: it.link as string,
      fonte: it.displayLink as string,
      snippet: (it.snippet as string) ?? "",
    }));
    return NextResponse.json({ configurado: true, resultados });
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : "Falha na busca";
    return NextResponse.json({ configurado: true, erro: msg }, { status: 200 });
  }
}
