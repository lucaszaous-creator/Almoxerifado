export type ItemMenu = {
  href: string;
  rotulo: string;
  icone: string;
  grupo: "Operação" | "Estoque" | "Gestão";
  gestor?: boolean;
  gerente?: boolean;
};

export const MENU: ItemMenu[] = [
  { href: "/", rotulo: "Painel", icone: "📊", grupo: "Operação" },
  { href: "/itens", rotulo: "Consultar itens", icone: "🔍", grupo: "Operação" },
  { href: "/scanner", rotulo: "Escanear", icone: "📷", grupo: "Operação" },
  { href: "/requisicoes", rotulo: "Requisições", icone: "📝", grupo: "Operação" },
  { href: "/compras", rotulo: "Compras", icone: "🛒", grupo: "Operação" },

  { href: "/movimentacoes", rotulo: "Movimentações", icone: "🔄", grupo: "Estoque", gestor: true },
  { href: "/atencoes", rotulo: "Atenções", icone: "⚠️", grupo: "Estoque", gestor: true },
  { href: "/fornecedores", rotulo: "Fornecedores", icone: "🏭", grupo: "Estoque", gestor: true },
  { href: "/melhores-precos", rotulo: "Melhores preços", icone: "💲", grupo: "Estoque", gestor: true },

  { href: "/relatorios", rotulo: "Relatórios", icone: "📈", grupo: "Gestão", gestor: true },
  { href: "/curva-abc", rotulo: "Curva ABC", icone: "🅰️", grupo: "Gestão", gestor: true },
  { href: "/auditoria", rotulo: "Auditoria", icone: "🕵️", grupo: "Gestão", gerente: true },
  { href: "/usuarios", rotulo: "Usuários", icone: "👥", grupo: "Gestão", gerente: true },
  { href: "/configuracoes", rotulo: "Configurações", icone: "⚙️", grupo: "Gestão", gerente: true },
];

export const GRUPOS: ItemMenu["grupo"][] = ["Operação", "Estoque", "Gestão"];
