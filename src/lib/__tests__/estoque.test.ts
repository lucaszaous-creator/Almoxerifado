import { describe, it, expect } from "vitest";
import { pontoReposicaoSugerido, estoqueMaximoSugerido, quantidadeReposicao } from "@/lib/estoque";

describe("sugestões de estoque", () => {
  it("ponto de reposição = consumo/dia × (prazo + segurança)", () => {
    expect(pontoReposicaoSugerido(2)).toBe(20); // 2 × 10
    expect(pontoReposicaoSugerido(1.5, 5, 5)).toBe(15); // 1.5 × 10
    expect(pontoReposicaoSugerido(0)).toBe(0);
  });

  it("estoque máximo cobre o ciclo", () => {
    expect(estoqueMaximoSugerido(2)).toBe(60); // 2 × 30
    expect(estoqueMaximoSugerido(0)).toBe(0);
  });

  it("quantidade de reposição nunca é negativa", () => {
    expect(quantidadeReposicao(5, 20)).toBe(15);
    expect(quantidadeReposicao(30, 20)).toBe(0);
  });
});
