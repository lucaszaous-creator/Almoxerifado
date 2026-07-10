import { describe, it, expect } from "vitest";
import { fmtNumero, fmtMoeda, fmtData, diasAteValidade } from "@/lib/format";

describe("format", () => {
  it("fmtNumero usa separadores pt-BR", () => {
    expect(fmtNumero(1234.5)).toBe("1.234,5");
    expect(fmtNumero(0)).toBe("0");
  });

  it("fmtMoeda formata em reais", () => {
    expect(fmtMoeda(1234.5)).toContain("1.234,50");
    expect(fmtMoeda(1234.5)).toContain("R$");
    expect(fmtMoeda(0)).toContain("0,00");
  });

  it("fmtData converte ISO para dd/mm/aaaa", () => {
    expect(fmtData("2026-03-09")).toBe("09/03/2026");
    expect(fmtData(null)).toBe("—");
  });

  it("diasAteValidade calcula a diferença em dias", () => {
    const hoje = new Date();
    const iso = (d: Date) => d.toISOString().slice(0, 10);
    const amanha = new Date(hoje);
    amanha.setDate(hoje.getDate() + 1);
    const ontem = new Date(hoje);
    ontem.setDate(hoje.getDate() - 1);

    expect(diasAteValidade(iso(hoje))).toBe(0);
    expect(diasAteValidade(iso(amanha))).toBe(1);
    expect(diasAteValidade(iso(ontem))).toBe(-1);
    expect(diasAteValidade(null)).toBeNull();
  });
});
