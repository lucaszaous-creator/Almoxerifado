import { barcodeSvg } from "@/lib/barcode";

export type DadosEtiqueta = {
  nome: string;
  codigo: string;
  codigo_barras?: string | null;
  localizacao?: string | null;
};

export default function Etiqueta({ item }: { item: DadosEtiqueta }) {
  const valor = (item.codigo_barras && item.codigo_barras.trim()) || item.codigo;
  const svg = barcodeSvg(valor, { modulo: 2, altura: 44 });

  return (
    <div className="etiqueta flex flex-col items-center justify-between rounded border border-slate-300 bg-white p-2 text-center text-black">
      <p className="line-clamp-2 w-full text-[11px] font-semibold leading-tight">{item.nome}</p>
      <div className="my-1 w-full" dangerouslySetInnerHTML={{ __html: svg }} />
      <p className="font-mono text-[10px] tracking-wide">{valor}</p>
      {item.localizacao && <p className="text-[9px] text-slate-600">📍 {item.localizacao}</p>}
    </div>
  );
}
