"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { criarClienteBrowser } from "@/lib/supabase/client";
import { useToast } from "@/components/ui/Toast";
import { UNIDADES, type Fornecedor, type Item } from "@/lib/tipos";

export default function NovoPedido({
  itens,
  fornecedores,
  usuarioId,
  itemInicial,
}: {
  itens: Item[];
  fornecedores: Fornecedor[];
  usuarioId: string;
  itemInicial?: string;
}) {
  const router = useRouter();
  const supabase = criarClienteBrowser();
  const toast = useToast();
  const inicial = itemInicial ? itens.find((i) => i.id === itemInicial) : undefined;
  const [aberto, setAberto] = useState(Boolean(inicial));
  const [itemId, setItemId] = useState(inicial?.id ?? "");
  const [descricao, setDescricao] = useState(inicial?.nome ?? "");
  const [quantidade, setQuantidade] = useState<number>(1);
  const [unidade, setUnidade] = useState(inicial?.unidade ?? "un");
  const [fornecedorId, setFornecedorId] = useState(inicial?.fornecedor_id ?? "");
  const [precoEstimado, setPrecoEstimado] = useState<number>(inicial?.preco_custo ?? 0);
  const [justificativa, setJustificativa] = useState("");
  const [anexo, setAnexo] = useState<File | null>(null);
  const [salvando, setSalvando] = useState(false);

  function escolherItem(id: string) {
    setItemId(id);
    const it = itens.find((i) => i.id === id);
    if (it) {
      setDescricao(it.nome);
      setUnidade(it.unidade);
      setFornecedorId(it.fornecedor_id ?? "");
      setPrecoEstimado(it.preco_custo ?? 0);
    }
  }

  async function enviar(e: React.FormEvent) {
    e.preventDefault();
    if (!descricao.trim()) return toast.erro("Informe o item.");
    if (quantidade <= 0) return toast.erro("Quantidade deve ser maior que zero.");
    setSalvando(true);

    let anexoUrl: string | null = null;
    if (anexo) {
      const ext = anexo.name.split(".").pop() ?? "pdf";
      const caminho = `pedidos/${usuarioId}/${Date.now()}.${ext}`;
      const up = await supabase.storage.from("itens").upload(caminho, anexo);
      if (!up.error) anexoUrl = supabase.storage.from("itens").getPublicUrl(caminho).data.publicUrl;
    }

    const { error } = await supabase.from("pedidos_compra").insert({
      item_id: itemId || null,
      descricao_item: descricao.trim(),
      quantidade_solicitada: quantidade,
      unidade,
      fornecedor_id: fornecedorId || null,
      preco_estimado: precoEstimado || 0,
      anexo_url: anexoUrl,
      justificativa: justificativa.trim() || null,
      solicitante_id: usuarioId,
    });
    setSalvando(false);
    if (error) return toast.erro(error.message);

    toast.sucesso("Pedido de compra enviado.");
    setItemId("");
    setDescricao("");
    setQuantidade(1);
    setJustificativa("");
    setAnexo(null);
    setAberto(false);
    router.refresh();
  }

  if (!aberto) {
    return (
      <button onClick={() => setAberto(true)} className="btn-primary">
        + Novo pedido de compra
      </button>
    );
  }

  return (
    <form onSubmit={enviar} className="card space-y-4 p-6">
      <h2 className="font-semibold text-slate-800">Novo pedido de compra</h2>

      <div>
        <label className="label">Item já cadastrado (opcional)</label>
        <select className="input" value={itemId} onChange={(e) => escolherItem(e.target.value)}>
          <option value="">— Item novo / não cadastrado —</option>
          {itens.map((i) => (
            <option key={i.id} value={i.id}>
              {i.nome} (estoque: {i.quantidade} {i.unidade})
            </option>
          ))}
        </select>
      </div>

      <div>
        <label className="label">Descrição do item *</label>
        <input
          className="input"
          value={descricao}
          onChange={(e) => setDescricao(e.target.value)}
          placeholder="O que precisa ser comprado?"
          required
        />
      </div>

      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <div>
          <label className="label">Quantidade *</label>
          <input
            className="input"
            type="number"
            min={0}
            step="any"
            value={quantidade}
            onChange={(e) => setQuantidade(Number(e.target.value))}
            required
          />
        </div>
        <div>
          <label className="label">Unidade</label>
          <select className="input" value={unidade} onChange={(e) => setUnidade(e.target.value)}>
            {UNIDADES.map((u) => (
              <option key={u} value={u}>
                {u}
              </option>
            ))}
          </select>
        </div>
        <div>
          <label className="label">Preço estimado (R$)</label>
          <input
            className="input"
            type="number"
            min={0}
            step="0.01"
            value={precoEstimado}
            onChange={(e) => setPrecoEstimado(Number(e.target.value))}
          />
        </div>
        <div>
          <label className="label">Fornecedor</label>
          <select className="input" value={fornecedorId} onChange={(e) => setFornecedorId(e.target.value)}>
            <option value="">Sem fornecedor</option>
            {fornecedores.map((f) => (
              <option key={f.id} value={f.id}>
                {f.nome}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div>
        <label className="label">Justificativa</label>
        <textarea
          className="input"
          rows={2}
          value={justificativa}
          onChange={(e) => setJustificativa(e.target.value)}
          placeholder="Por que é necessário? Para qual setor/serviço?"
        />
      </div>

      <div>
        <label className="label">Anexo (orçamento / nota) — opcional</label>
        <input
          type="file"
          className="text-sm text-slate-600"
          onChange={(e) => setAnexo(e.target.files?.[0] ?? null)}
        />
      </div>

      <div className="flex gap-3">
        <button type="submit" className="btn-primary" disabled={salvando}>
          {salvando ? "Enviando..." : "Enviar pedido"}
        </button>
        <button type="button" className="btn-secondary" onClick={() => setAberto(false)}>
          Cancelar
        </button>
      </div>
    </form>
  );
}
