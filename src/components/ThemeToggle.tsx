"use client";

import { useEffect, useState } from "react";

export default function ThemeToggle() {
  const [escuro, setEscuro] = useState(false);

  useEffect(() => {
    setEscuro(document.documentElement.classList.contains("dark"));
  }, []);

  function alternar() {
    const novo = !escuro;
    setEscuro(novo);
    document.documentElement.classList.toggle("dark", novo);
    try {
      localStorage.setItem("tema", novo ? "escuro" : "claro");
    } catch {
      /* ignore */
    }
  }

  return (
    <button
      onClick={alternar}
      className="rounded-full p-2 text-slate-600 hover:bg-slate-100"
      title={escuro ? "Tema claro" : "Tema escuro"}
      aria-label="Alternar tema"
    >
      {escuro ? "☀️" : "🌙"}
    </button>
  );
}
