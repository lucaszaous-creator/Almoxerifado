import { describe, it, expect } from "vitest";
import { sanitizarBusca, sanitizarCodigo } from "@/lib/db";
import { montarConsulta, urlGoogle } from "@/lib/busca";
import { precisaRepor, podeGerenciar } from "@/lib/tipos";

describe("sanitização", () => {
  it("sanitizarBusca remove caracteres que quebram o filtro", () => {
    expect(sanitizarBusca("parafuso, (6mm) %")).toBe("parafuso 6mm");
    expect(sanitizarBusca("  a   b  ")).toBe("a b");
  });
  it("sanitizarCodigo remove vírgulas/parênteses", () => {
    expect(sanitizarCodigo("789(12),3 ")).toBe("789123");
  });
});

describe("busca web", () => {
  it("montarConsulta inclui nome e termos B2B", () => {
    const q = montarConsulta({ nome: "Parafuso", marca: "X" }, true);
    expect(q).toContain("Parafuso");
    expect(q).toContain("X");
    expect(q).toContain("CNPJ");
  });
  it("urlGoogle codifica a consulta", () => {
    expect(urlGoogle("a b")).toContain("q=a%20b");
  });
});

describe("regra de reposição", () => {
  it("precisaRepor considera ponto de reposição e mínimo", () => {
    expect(precisaRepor({ quantidade: 5, estoque_minimo: 10, ponto_reposicao: 0 })).toBe(true);
    expect(precisaRepor({ quantidade: 5, estoque_minimo: 0, ponto_reposicao: 8 })).toBe(true);
    expect(precisaRepor({ quantidade: 20, estoque_minimo: 10, ponto_reposicao: 8 })).toBe(false);
    expect(precisaRepor({ quantidade: 5, estoque_minimo: 0, ponto_reposicao: 0 })).toBe(false);
  });
  it("podeGerenciar só para gerente/almoxarife", () => {
    expect(podeGerenciar("gerente")).toBe(true);
    expect(podeGerenciar("almoxarife")).toBe(true);
    expect(podeGerenciar("funcionario")).toBe(false);
    expect(podeGerenciar(null)).toBe(false);
  });
});
