// Gerador de código de barras Code 128 (subset B) em SVG puro, sem dependências.

const PATTERNS = [
  "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
  "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
  "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
  "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
  "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
  "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
  "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
  "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
  "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
  "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
  "114131", "311141", "411131", "211412", "211214", "211232", "2331112",
];

const START_B = 104;
const STOP = 106;

/** Retorna a sequência de larguras (módulos) das barras/espaços para Code128-B. */
function encodar(texto: string): number[] {
  const limpo = texto.replace(/[^\x20-\x7e]/g, "").slice(0, 40) || " ";
  const codes = [START_B];
  let checksum = START_B;
  for (let i = 0; i < limpo.length; i++) {
    const valor = limpo.charCodeAt(i) - 32;
    codes.push(valor);
    checksum += valor * (i + 1);
  }
  codes.push(checksum % 103);
  codes.push(STOP);

  const larguras: number[] = [];
  for (const c of codes) {
    for (const ch of PATTERNS[c]) larguras.push(parseInt(ch, 10));
  }
  return larguras;
}

/**
 * Gera um SVG (string) do código de barras.
 * @param texto conteúdo a codificar (SKU ou EAN)
 * @param opts largura do módulo e altura das barras
 */
export function barcodeSvg(
  texto: string,
  opts: { modulo?: number; altura?: number } = {}
): string {
  const modulo = opts.modulo ?? 2;
  const altura = opts.altura ?? 48;
  const larguras = encodar(texto);
  const total = larguras.reduce((s, w) => s + w, 0) * modulo;

  let x = 0;
  let barra = true; // começa com barra
  const rects: string[] = [];
  for (const w of larguras) {
    const largura = w * modulo;
    if (barra) {
      rects.push(`<rect x="${x}" y="0" width="${largura}" height="${altura}" fill="#000"/>`);
    }
    x += largura;
    barra = !barra;
  }

  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${total} ${altura}" width="${total}" height="${altura}" shape-rendering="crispEdges" preserveAspectRatio="xMidYMid meet">${rects.join("")}</svg>`;
}
