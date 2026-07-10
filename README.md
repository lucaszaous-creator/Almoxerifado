# 📦 Almoxarifado & Compras

Sistema web para controle do almoxarifado e pedidos de compra, pensado para
reduzir o tempo perdido pelos funcionários procurando itens e dar visibilidade
ao **gerente** e ao **responsável pelo almoxarifado (almoxarife)**.

## Por que existe

- Funcionários perdiam tempo indo ao almoxarifado sem saber se o item existia.
- Não havia consulta rápida de itens, quantidades e localização.
- Faltava controle de estoque mínimo e de datas de validade.
- Ninguém era avisado quando um item estava acabando.

Este app resolve isso com **consulta online**, **controle de estoque**,
**alertas automáticos** e **notificações in-app** para gerente e almoxarife.

## Principais funcionalidades

- 🔍 **Consulta de itens** — qualquer funcionário busca por nome, código,
  categoria ou localização, sem precisar ir ao almoxarifado.
- 📦 **Cadastro e organização** — itens com categoria, unidade, localização
  (setor/prateleira), estoque mínimo e validade.
- 🔄 **Movimentações** — entradas, saídas e ajustes com histórico completo e
  responsável registrado. Saída bloqueia estoque negativo.
- ⚠️ **Alerta de estoque baixo** — ao chegar no mínimo, gerente e almoxarife
  são notificados automaticamente.
- ⏳ **Controle de validade** — itens vencendo (≤ 30 dias) ou vencidos geram
  alertas.
- 📝 **Requisições de material** — funcionários solicitam itens do estoque
  (vários de uma vez, por setor); o almoxarife aprova e **atende**, e a baixa
  de estoque é lançada automaticamente (com rollback se faltar estoque).
- 🛒 **Pedidos de compra** — funcionários solicitam itens em falta; o fluxo vai
  de *pendente → aprovado → comprado → recebido*. Ao receber um item já
  cadastrado, a entrada no estoque é lançada automaticamente.
- ⚠️ **Atenções** — painel único com tudo que precisa de ação: sem estoque,
  estoque baixo, itens vencidos e vencendo, com atalho para comprar.
- 🏭 **Fornecedores** — cadastro de fornecedores vinculável a itens e compras.
- 💰 **Custo e valorização** — custo unitário por item e **valor total do
  estoque** no painel e nos relatórios.
- 🖼️ **Fotos dos itens** — upload de imagem por item (armazenada no Supabase
  Storage) exibida na consulta e no detalhe.
- 🔎 **Busca global** — campo no topo que encontra qualquer item por nome,
  código ou localização de qualquer tela.
- 📈 **Relatórios** — resumo por categoria (com valor) e exportação em CSV
  (estoque, movimentações, compras e requisições) compatível com Excel.
- 🔔 **Notificações dentro do sistema** — sino com contador em tempo real e
  painel dedicado; feedback de ações via *toasts* e confirmação em ações
  sensíveis.

### Recursos avançados

- **Custo médio ponderado** recalculado automaticamente a cada entrada.
- **Ponto de reposição** e **estoque máximo** com **sugestão e geração
  automática de pedidos** de compra.
- **Recebimento parcial** de pedidos e **atendimento parcial** de requisições.
- **Número de pedido (PO#)**, preço estimado, fornecedor e **anexo** (NF/orçamento).
- **Centro de custo** nas requisições.
- **Kardex** (ficha de estoque com saldo corrido) e **Curva ABC**.
- **Catálogo de preços por fornecedor** (cotações) com destaque do melhor preço.
- **Código de barras, marca, fabricante, observações internas** e
  **ativar/inativar** item.
- **Motivos de movimentação** (perda, quebra, vencimento, devolução, consumo…).
- **Importação de itens via CSV** e exportações em CSV.
- **Filtros avançados e ordenação** na consulta + **busca global** no topo.
- **Gráfico de movimentações (14 dias)** e **top itens consumidos** no painel.
- **Configurações da empresa** (nome, moeda, dias de alerta de validade).
- **Log de auditoria** (quem fez o quê) e **perfil do usuário** (nome/senha).
- **Tema claro/escuro**.
- **Rotina diária automática** (pg_cron): gera os alertas de validade e um
  resumo de reposição todo dia, mesmo sem ninguém abrir o app.
- **Sugestão inteligente de parâmetros**: ponto de reposição e estoque máximo
  sugeridos a partir do consumo, exibidos na ficha do item.
- **Previsão de consumo**: consumo médio diário e **dias de cobertura** por
  item; o painel destaca os itens que **vão acabar em breve** e a ficha mostra
  a cobertura estimada.
- **CI** (GitHub Actions): lint + testes + build a cada push/PR.
- Atalho de teclado **`/`** para focar a busca.
- **Ações rápidas** no painel e navegação por seções (Operação/Estoque/Gestão).
- **Testes automatizados** (Vitest) das funções puras: `npm test`.
- **Controle por lote** (validade e saldo por lote) com **saída FEFO** (o lote
  que vence primeiro sai primeiro) e alertas de validade por lote.
- **Melhores preços (CNPJ/B2B)**: catálogo de preços por fornecedor com
  **preço por lote e por unidade**, comparação entre fornecedores que atendem
  CNPJ e geração do pedido para o de menor preço.
- **Etiquetas de código de barras**: geração e impressão de etiquetas (Code
  128) com nome, código de barras e localização (sem dependências externas).
- **Leitura por câmera** (Android e **iPhone**): escaneie o código de barras
  pelo celular (biblioteca ZXing, carregada sob demanda) para localizar o item
  e **dar entrada/saída na própria tela do scanner**; com entrada manual como
  alternativa.
- **Pesquisa de preços na web**: a partir do nome/marca/código de barras do
  produto, abre o **Google**, **Google Shopping** e **Mercado Livre** já
  pesquisando (com foco em CNPJ/atacado). Opcionalmente, com
  `GOOGLE_CSE_KEY` e `GOOGLE_CSE_CX` configurados, mostra os resultados
  **dentro do app** (Google Programmable Search / Custom Search JSON API).
- 👥 **Papéis** — `gerente`, `almoxarife` e `funcionario`, com permissões
  aplicadas no banco (RLS).

## Papéis e permissões

| Ação                                   | Gerente | Almoxarife | Funcionário |
|----------------------------------------|:-------:|:----------:|:-----------:|
| Consultar itens                        |   ✅    |     ✅     |     ✅      |
| Abrir requisição de material           |   ✅    |     ✅     |     ✅      |
| Abrir pedido de compra                 |   ✅    |     ✅     |     ✅      |
| Cadastrar / editar itens               |   ✅    |     ✅     |     —       |
| Movimentar estoque                     |   ✅    |     ✅     |     —       |
| Aprovar / atender requisições          |   ✅    |     ✅     |     —       |
| Aprovar / receber pedidos              |   ✅    |     ✅     |     —       |
| Atenções e relatórios                  |   ✅    |     ✅     |     —       |
| Gerenciar usuários (papéis)            |   ✅    |     —      |     —       |

> O **primeiro usuário cadastrado** vira `gerente` automaticamente. Ele define
> os papéis dos demais em **Usuários**.

## Stack

- [Next.js 14](https://nextjs.org/) (App Router, TypeScript)
- [Tailwind CSS](https://tailwindcss.com/)
- [Supabase](https://supabase.com/) — Postgres, Auth e Realtime

## Como rodar localmente

1. **Instale as dependências**
   ```bash
   npm install
   ```

2. **Configure o banco (Supabase)**
   - Crie um projeto em https://supabase.com.
   - Aplique as migrations de `supabase/migrations/` (na ordem) pelo
     **SQL Editor** do painel, ou via Supabase CLI:
     ```bash
     supabase link --project-ref SEU_REF
     supabase db push
     ```

3. **Variáveis de ambiente**
   ```bash
   cp .env.local.example .env.local
   ```
   Preencha com os dados de *Project Settings → API*:
   ```
   NEXT_PUBLIC_SUPABASE_URL=https://SEU-PROJETO.supabase.co
   NEXT_PUBLIC_SUPABASE_ANON_KEY=sua-anon-key
   ```

4. **Realtime (para o sino de notificações)**
   No painel Supabase → *Database → Replication*, habilite a tabela
   `notificacoes` na publicação `supabase_realtime` (ou rode
   `alter publication supabase_realtime add table notificacoes;`).

5. **Confirmação de e-mail (importante no primeiro acesso)**
   Por padrão o Supabase exige confirmação de e-mail no cadastro. Para uso
   interno rápido, no painel Supabase → *Authentication → Sign In / Providers
   → Email*, desative **"Confirm email"**. Assim o primeiro cadastro já entra
   direto e vira **gerente**. (O perfil é criado no cadastro de qualquer forma;
   sem confirmar o e-mail, porém, a sessão não é iniciada.)

6. **Rode**
   ```bash
   npm run dev
   ```
   Acesse http://localhost:3000, crie sua conta (primeiro usuário = gerente).

## Deploy (Vercel/Netlify)

Basta configurar as duas variáveis de ambiente no painel do provedor:

```
NEXT_PUBLIC_SUPABASE_URL
NEXT_PUBLIC_SUPABASE_ANON_KEY
```

e apontar o build para `npm run build`. O banco (Supabase) é o mesmo em
qualquer ambiente.

## Estrutura

```
src/
├── app/
│   ├── login/                 # tela de login/cadastro
│   └── (app)/                 # área autenticada (sidebar + sino)
│       ├── page.tsx           # painel (dashboard)
│       ├── itens/             # consulta, cadastro, detalhe, edição
│       ├── requisicoes/       # requisições de material
│       ├── compras/           # pedidos de compra
│       ├── movimentacoes/     # histórico geral
│       ├── atencoes/          # alertas de estoque e validade
│       ├── relatorios/        # resumo + exportação CSV
│       ├── notificacoes/      # painel de notificações
│       └── usuarios/          # gestão de papéis (gerente)
├── components/                # componentes de UI
└── lib/                       # clientes Supabase, tipos, helpers
supabase/migrations/           # schema, triggers, RLS e seed
```

## Como as notificações funcionam

- **Estoque baixo, novo pedido, aprovação/recebimento**: disparadas por
  *triggers* no banco (`supabase/migrations/0001_schema_inicial.sql`).
- **Validade próxima/vencida**: geradas pela função `gerar_alertas_validade()`,
  chamada ao abrir o painel (idempotente — no máximo 1 alerta por item/tipo a
  cada 24h). Para automatizar sem depender de acessos, é possível agendar essa
  função com `pg_cron`.
