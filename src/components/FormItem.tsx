"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";
import PesquisaPrecos from "@/components/PesquisaPrecos";
import { UNIDADES, type Categoria, type Fornecedor, type Item } from "@/lib/tipos";

export default function FormItem({
  categorias,
  fornecedores,
  item,
  usuarioId,
}: {
  categorias: Categoria[];
  fornecedores: Fornecedor[];
  item?: Item;
  usuarioId: string;
}) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const toast = useToast();
  const edicao = Boolean(item);

  const [form, setForm] = useState({
    codigo: item?.codigo ?? "",
    codigo_barras: item?.codigo_barras ?? "",
    nome: item?.nome ?? "",
    descricao: item?.descricao ?? "",
    categoria_id: item?.categoria_id ?? "",
    fornecedor_id: item?.fornecedor_id ?? "",
    marca: item?.marca ?? "",
    fabricante: item?.fabricante ?? "",
    unidade: item?.unidade ?? "un",
    localizacao: item?.localizacao ?? "",
    quantidade: item?.quantidade ?? 0,
    estoque_minimo: item?.estoque_minimo ?? 0,
    ponto_reposicao: item?.ponto_reposicao ?? 0,
    estoque_maximo: item?.estoque_maximo ?? 0,
    preco_custo: item?.preco_custo ?? 0,
    data_validade: item?.data_validade ?? "",
    observacoes_internas: item?.observacoes_internas ?? "",
    ativo: item?.ativo ?? true,
    controla_lote: item?.controla_lote ?? false,
  });
  const [imagemUrl, setImagemUrl] = useState<string | null>(item?.imagem_url ?? null);
  const [enviandoImg, setEnviandoImg] = useState(false);
  const [salvando, setSalvando] = useState(false);

  function set<K extends keyof typeof form>(k: K, v: (typeof form)[K]) {
    setForm((f) => ({ ...f, [k]: v }));
  }

  async function enviarImagem(e: React.ChangeEvent<HTMLInputElement>) {
    const arquivo = e.target.files?.[0];
    if (!arquivo) return;
    if (arquivo.size > 5 * 1024 * 1024) {
      toast.erro("Imagem muito grande (máx. 5 MB).");
      return;
    }
    setEnviandoImg(true);
    const ext = arquivo.name.split(".").pop() ?? "jpg";
    const caminho = `${usuarioId}/${Date.now()}.${ext}`;
    const { error } = await supabase.storage.from("itens").upload(caminho, arquivo, {
      cacheControl: "3600",
      upsert: false,
    });
    setEnviandoImg(false);
    if (error) {
      toast.erro("Falha ao enviar imagem: " + error.message);
      return;
    }
    const { data } = supabase.storage.from("itens").getPublicUrl(caminho);
    setImagemUrl(data.publicUrl);
    toast.sucesso("Imagem enviada.");
  }

  async function salvar(e: React.FormEvent) {
    e.preventDefault();
    setSalvando(true);

    const payload = {
      codigo: form.codigo.trim() || gerarCodigo(form.nome),
      codigo_barras: form.codigo_barras.trim() || null,
      nome: form.nome.trim(),
      descricao: form.descricao.trim() || null,
      categoria_id: form.categoria_id || null,
      fornecedor_id: form.fornecedor_id || null,
      marca: form.marca.trim() || null,
      fabricante: form.fabricante.trim() || null,
      unidade: form.unidade,
      localizacao: form.localizacao.trim() || null,
      estoque_minimo: Number(form.estoque_minimo) || 0,
      ponto_reposicao: Number(form.ponto_reposicao) || 0,
      estoque_maximo: Number(form.estoque_maximo) || 0,
      preco_custo: Number(form.preco_custo) || 0,
      data_validade: form.data_validade || null,
      observacoes_internas: form.observacoes_internas.trim() || null,
      ativo: form.ativo,
      controla_lote: form.controla_lote,
      imagem_url: imagemUrl,
    };

    try {
      if (edicao && item) {
        const { error } = await supabase.from("itens").update(payload).eq("id", item.id);
        if (error) throw error;
        toast.sucesso("Item atualizado.");
        router.push(`/itens/${item.id}`);
      } else {
        const { data: novo, error } = await supabase
          .from("itens")
          .insert({ ...payload, quantidade: 0, criado_por: usuarioId })
          .select()
          .single();
        if (error) throw error;

        const qtdInicial = Number(form.quantidade) || 0;
        if (qtdInicial > 0 && novo) {
          await supabase.from("movimentacoes").insert({
            item_id: novo.id,
            tipo: "entrada",
            quantidade: qtdInicial,
            observacao: "Estoque inicial (cadastro)",
            usuario_id: usuarioId,
          });
        }
        toast.sucesso("Item cadastrado.");
        router.push(`/itens/${novo!.id}`);
      }
      router.refresh();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      toast.erro(msg.includes("duplicate") ? "Já existe um item com este código." : msg);
      setSalvando(false);
    }
  }

  return (
    <form onSubmit={salvar} className="card space-y-5 p-6">
      <div className="flex flex-col gap-5 sm:flex-row">
        {/* Imagem */}
        <div className="shrink-0">
          <label className="label">Foto do item</label>
          <div className="relative flex h-32 w-32 items-center justify-center overflow-hidden rounded-xl border border-dashed border-slate-300 bg-slate-50">
            {imagemUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img src={imagemUrl} alt="Prévia" className="h-full w-full object-cover" />
            ) : (
              <span className="text-3xl text-slate-300">📷</span>
            )}
          </div>
          <label className="mt-2 block cursor-pointer text-center text-xs font-medium text-brand-600 hover:underline">
            {enviandoImg ? "Enviando..." : imagemUrl ? "Trocar foto" : "Enviar foto"}
            <input type="file" accept="image/*" className="hidden" onChange={enviarImagem} disabled={enviandoImg} />
          </label>
          {imagemUrl && (
            <button
              type="button"
              onClick={() => setImagemUrl(null)}
              className="mt-1 block w-full text-center text-xs text-slate-400 hover:text-rose-600"
            >
              Remover
            </button>
          )}
        </div>

        {/* Campos principais */}
        <div className="flex-1 space-y-4">
          <div>
            <label className="label">Nome do item *</label>
            <input
              className="input"
              value={form.nome}
              onChange={(e) => set("nome", e.target.value)}
              required
              placeholder="Ex.: Parafuso sextavado 6mm"
            />
          </div>
          <div className="grid gap-4 sm:grid-cols-2">
            <div>
              <label className="label">Código / SKU</label>
              <input
                className="input"
                value={form.codigo}
                onChange={(e) => set("codigo", e.target.value)}
                placeholder="Gerado automaticamente se vazio"
              />
            </div>
            <div>
              <label className="label">Código de barras</label>
              <input
                className="input"
                value={form.codigo_barras}
                onChange={(e) => set("codigo_barras", e.target.value)}
                placeholder="EAN / GTIN"
              />
            </div>
            <div>
              <label className="label">Categoria</label>
              <select className="input" value={form.categoria_id} onChange={(e) => set("categoria_id", e.target.value)}>
                <option value="">Sem categoria</option>
                {categorias.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.nome}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>
      </div>

      <div>
        <label className="label">Descrição</label>
        <textarea className="input" rows={2} value={form.descricao} onChange={(e) => set("descricao", e.target.value)} />
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <div>
          <label className="label">Marca</label>
          <input className="input" value={form.marca} onChange={(e) => set("marca", e.target.value)} />
        </div>
        <div>
          <label className="label">Fabricante</label>
          <input className="input" value={form.fabricante} onChange={(e) => set("fabricante", e.target.value)} />
        </div>
        <div>
          <label className="label">Localização (setor/prateleira)</label>
          <input
            className="input"
            value={form.localizacao}
            onChange={(e) => set("localizacao", e.target.value)}
            placeholder="Ex.: Corredor B, Prateleira 3"
          />
        </div>
        <div>
          <label className="label">Fornecedor</label>
          <select className="input" value={form.fornecedor_id} onChange={(e) => set("fornecedor_id", e.target.value)}>
            <option value="">Sem fornecedor</option>
            {fornecedores.map((f) => (
              <option key={f.id} value={f.id}>
                {f.nome}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label className="label">Unidade</label>
          <select className="input" value={form.unidade} onChange={(e) => set("unidade", e.target.value)}>
            {UNIDADES.map((u) => (
              <option key={u} value={u}>
                {u}
              </option>
            ))}
          </select>
        </div>

        {!edicao && (
          <div>
            <label className="label">Quantidade inicial</label>
            <input
              className="input"
              type="number"
              min={0}
              step="any"
              value={form.quantidade}
              onChange={(e) => set("quantidade", Number(e.target.value))}
            />
          </div>
        )}
        <div>
          <label className="label">Estoque mínimo</label>
          <input
            className="input"
            type="number"
            min={0}
            step="any"
            value={form.estoque_minimo}
            onChange={(e) => set("estoque_minimo", Number(e.target.value))}
          />
        </div>
        <div>
          <label className="label">Ponto de reposição</label>
          <input
            className="input"
            type="number"
            min={0}
            step="any"
            value={form.ponto_reposicao}
            onChange={(e) => set("ponto_reposicao", Number(e.target.value))}
          />
        </div>
        <div>
          <label className="label">Estoque máximo</label>
          <input
            className="input"
            type="number"
            min={0}
            step="any"
            value={form.estoque_maximo}
            onChange={(e) => set("estoque_maximo", Number(e.target.value))}
          />
        </div>
        <div>
          <label className="label">Custo unitário (R$)</label>
          <input
            className="input"
            type="number"
            min={0}
            step="0.01"
            value={form.preco_custo}
            onChange={(e) => set("preco_custo", Number(e.target.value))}
          />
        </div>
        <div>
          <label className="label">Data de validade</label>
          <input
            className="input"
            type="date"
            value={form.data_validade ?? ""}
            onChange={(e) => set("data_validade", e.target.value)}
          />
        </div>
      </div>

      <div className="rounded-lg border border-slate-200 p-4">
        <label className="label">Pesquisar preço na web</label>
        <PesquisaPrecos
          compacto
          produto={{
            nome: form.nome,
            marca: form.marca,
            fabricante: form.fabricante,
            codigo_barras: form.codigo_barras,
          }}
        />
      </div>

      <div>
        <label className="label">Observações internas</label>
        <textarea
          className="input"
          rows={2}
          value={form.observacoes_internas}
          onChange={(e) => set("observacoes_internas", e.target.value)}
          placeholder="Notas visíveis apenas para a gestão"
        />
      </div>

      <div className="space-y-2">
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            checked={form.ativo}
            onChange={(e) => set("ativo", e.target.checked)}
            className="h-4 w-4 rounded border-slate-300"
          />
          Item ativo (desmarque para descontinuar sem excluir o histórico)
        </label>
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            checked={form.controla_lote}
            onChange={(e) => set("controla_lote", e.target.checked)}
            className="h-4 w-4 rounded border-slate-300"
          />
          Controlar por lote (validade e saldo por lote — recomendado p/ perecíveis)
        </label>
      </div>

      <div className="flex gap-3">
        <button type="submit" className="btn-primary" disabled={salvando || enviandoImg}>
          {salvando ? "Salvando..." : edicao ? "Salvar alterações" : "Cadastrar item"}
        </button>
        <button type="button" className="btn-secondary" onClick={() => router.back()}>
          Cancelar
        </button>
      </div>
    </form>
  );
}

function gerarCodigo(nome: string): string {
  const base = nome
    .toUpperCase()
    .replace(/[^A-Z0-9]/g, "")
    .slice(0, 4);
  const rand = Math.floor(1000 + Math.random() * 9000);
  return `${base || "ITEM"}-${rand}`;
}
