import { describe, it, expect } from "vitest";
import { barcodeSvg } from "@/lib/barcode";

describe("barcodeSvg (Code 128)", () => {
  it("gera um SVG com barras", () => {
    const svg = barcodeSvg("ABC123");
    expect(svg.startsWith("<svg")).toBe(true);
    expect(svg).toContain("<rect");
  });

  it("largura corresponde à contagem de módulos do Code 128", () => {
    // Code 128: total de módulos = 11*(n+2) + 13 (start + dados + checksum + stop)
    const modulo = 2;
    for (const texto of ["12345", "ABC-99"]) {
      const esperado = (11 * (texto.length + 2) + 13) * modulo;
      const svg = barcodeSvg(texto, { modulo });
      expect(svg).toContain(`width="${esperado}"`);
    }
  });

  it("não quebra com string vazia", () => {
    expect(() => barcodeSvg("")).not.toThrow();
  });
});
