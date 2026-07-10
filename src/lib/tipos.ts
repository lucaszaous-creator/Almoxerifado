export type PapelUsuario = "gerente" | "almoxarife" | "funcionario";

export type TipoMovimentacao = "entrada" | "saida" | "ajuste";

export type StatusPedido =
  | "pendente"
  | "aprovado"
  | "rejeitado"
  | "comprado"
  | "parcial"
  | "recebido"
  | "cancelado";

export type StatusRequisicao =
  | "pendente"
  | "aprovada"
  | "rejeitada"
  | "parcial"
  | "atendida"
  | "cancelada";

export type TipoNotificacao =
  | "estoque_baixo"
  | "validade_proxima"
  | "validade_vencida"
  | "novo_pedido"
  | "pedido_aprovado"
  | "pedido_rejeitado"
  | "pedido_recebido"
  | "nova_requisicao"
  | "requisicao_aprovada"
  | "requisicao_rejeitada"
  | "requisicao_atendida";

export interface Profile {
  id: string;
  nome: string;
  email: string | null;
  papel: PapelUsuario;
  ativo: boolean;
  criado_em: string;
}

export interface Categoria {
  id: string;
  nome: string;
  descricao: string | null;
  criado_em: string;
}

export interface Fornecedor {
  id: string;
  nome: string;
  cnpj: string | null;
  contato: string | null;
  telefone: string | null;
  email: string | null;
  observacao: string | null;
  fornece_cnpj: boolean;
  criado_em: string;
}

export interface FornecedorItem {
  id: string;
  fornecedor_id: string;
  item_id: string;
  preco: number;
  quantidade_lote: number;
  prazo_entrega_dias: number | null;
  codigo_fornecedor: string | null;
  criado_em: string;
  fornecedores?: (Pick<Fornecedor, "nome" | "fornece_cnpj" | "cnpj">) | null;
  itens?: Pick<Item, "nome" | "codigo" | "unidade"> | null;
}

export interface Lote {
  id: string;
  item_id: string;
  codigo_lote: string;
  data_validade: string | null;
  quantidade_inicial: number;
  quantidade_atual: number;
  custo_unitario: number;
  fornecedor_id: string | null;
  criado_em: string;
  fornecedores?: Pick<Fornecedor, "nome"> | null;
}

export interface Item {
  id: string;
  codigo: string;
  codigo_barras: string | null;
  nome: string;
  descricao: string | null;
  categoria_id: string | null;
  unidade: string;
  localizacao: string | null;
  quantidade: number;
  estoque_minimo: number;
  ponto_reposicao: number;
  estoque_maximo: number;
  data_validade: string | null;
  preco_custo: number;
  custo_medio: number;
  fornecedor_id: string | null;
  imagem_url: string | null;
  marca: string | null;
  fabricante: string | null;
  observacoes_internas: string | null;
  ativo: boolean;
  controla_lote: boolean;
  criado_por: string | null;
  criado_em: string;
  atualizado_em: string;
  categorias?: Categoria | null;
  fornecedores?: Pick<Fornecedor, "nome"> | null;
}

export interface Movimentacao {
  id: string;
  item_id: string;
  tipo: TipoMovimentacao;
  quantidade: number;
  motivo: string | null;
  custo_unitario: number | null;
  lote_id: string | null;
  observacao: string | null;
  usuario_id: string | null;
  criado_em: string;
  itens?: Pick<Item, "nome" | "codigo" | "unidade"> | null;
  profiles?: Pick<Profile, "nome"> | null;
}

export interface PedidoCompra {
  id: string;
  numero: number | null;
  item_id: string | null;
  descricao_item: string;
  quantidade_solicitada: number;
  quantidade_recebida: number;
  unidade: string | null;
  justificativa: string | null;
  status: StatusPedido;
  preco_estimado: number;
  fornecedor_id: string | null;
  anexo_url: string | null;
  solicitante_id: string | null;
  aprovador_id: string | null;
  observacao_gestor: string | null;
  criado_em: string;
  atualizado_em: string;
  solicitante?: Pick<Profile, "nome"> | null;
  fornecedores?: Pick<Fornecedor, "nome"> | null;
}

export interface RequisicaoItem {
  id: string;
  requisicao_id: string;
  item_id: string;
  quantidade: number;
  quantidade_atendida: number;
  itens?: Pick<Item, "nome" | "codigo" | "unidade" | "quantidade"> | null;
}

export interface Requisicao {
  id: string;
  solicitante_id: string | null;
  setor: string | null;
  centro_custo: string | null;
  justificativa: string | null;
  status: StatusRequisicao;
  aprovador_id: string | null;
  observacao_gestor: string | null;
  criado_em: string;
  atualizado_em: string;
  solicitante?: Pick<Profile, "nome"> | null;
  requisicao_itens?: RequisicaoItem[];
}

export interface Notificacao {
  id: string;
  usuario_id: string;
  tipo: TipoNotificacao;
  titulo: string;
  mensagem: string;
  lida: boolean;
  item_id: string | null;
  pedido_id: string | null;
  criado_em: string;
}

export interface Configuracao {
  id: boolean;
  empresa_nome: string;
  moeda: string;
  dias_alerta_validade: number;
  atualizado_em: string;
}

export interface Auditoria {
  id: string;
  usuario_id: string | null;
  acao: string;
  entidade: string;
  entidade_id: string | null;
  descricao: string | null;
  criado_em: string;
  profiles?: Pick<Profile, "nome"> | null;
}

export const ROTULO_PAPEL: Record<PapelUsuario, string> = {
  gerente: "Gerente",
  almoxarife: "Almoxarife",
  funcionario: "Funcionário",
};

export const ROTULO_STATUS: Record<StatusPedido, string> = {
  pendente: "Pendente",
  aprovado: "Aprovado",
  rejeitado: "Rejeitado",
  comprado: "Comprado",
  parcial: "Recebido parcial",
  recebido: "Recebido",
  cancelado: "Cancelado",
};

export const ROTULO_STATUS_REQ: Record<StatusRequisicao, string> = {
  pendente: "Pendente",
  aprovada: "Aprovada",
  rejeitada: "Rejeitada",
  parcial: "Atendida parcial",
  atendida: "Atendida",
  cancelada: "Cancelada",
};

export const UNIDADES = ["un", "cx", "pct", "kg", "g", "l", "ml", "m", "par", "rolo"];

/** Motivos de movimentação de estoque. */
export const MOTIVOS_SAIDA = ["Consumo", "Requisição", "Perda", "Quebra", "Vencimento", "Transferência", "Outro"];
export const MOTIVOS_ENTRADA = ["Compra", "Devolução", "Ajuste de inventário", "Estoque inicial", "Outro"];

export function podeGerenciar(papel?: PapelUsuario | null): boolean {
  return papel === "gerente" || papel === "almoxarife";
}

/** Classificação de reposição do item. */
export function precisaRepor(i: Pick<Item, "quantidade" | "estoque_minimo" | "ponto_reposicao">): boolean {
  const gatilho = Math.max(i.ponto_reposicao || 0, i.estoque_minimo || 0);
  return gatilho > 0 && i.quantidade <= gatilho;
}
