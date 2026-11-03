// Gera a documentação revisada (v2) do PIT Engenharia de Software II - Loja de Cupcakes
// Uso: node gerar_documentacao.js <saida.docx>
const fs = require("fs");
const path = require("path");
const {
  Document, Packer, Paragraph, TextRun, HeadingLevel, Table, TableRow, TableCell,
  WidthType, ShadingType, BorderStyle, AlignmentType, ImageRun, LevelFormat,
  PageBreak, Footer, PageNumber, TableOfContents,
} = require("docx");

const ROOT = path.resolve(__dirname, "..");
const OUT = process.argv[2] || path.join(ROOT, "entregas", "PIT_Software_II_Documentacao_Cupcakes_v3.docx");
const TEAL = "2F6F6C";
const W = 9026; // largura útil A4 com margens de 2,5 cm (DXA)

// ---------- helpers ----------
const runs = (t, base = {}) => t.split(/(\*\*.*?\*\*)/).filter(Boolean).map(p =>
  p.startsWith("**") ? new TextRun({ ...base, text: p.slice(2, -2), bold: true }) : new TextRun({ ...base, text: p }));
const P = (t, o = {}) => new Paragraph({ children: runs(t), spacing: { after: 120, line: 300 }, alignment: AlignmentType.JUSTIFIED, ...o });
const H1 = t => new Paragraph({ heading: HeadingLevel.HEADING_1, children: [new TextRun(t)] });
const H2 = t => new Paragraph({ heading: HeadingLevel.HEADING_2, children: [new TextRun(t)] });
const B = t => new Paragraph({ numbering: { reference: "bul", level: 0 }, children: runs(t), spacing: { after: 60, line: 288 } });
const N = (t, ref = "num") => new Paragraph({ numbering: { reference: ref, level: 0 }, children: runs(t), spacing: { after: 60, line: 288 } });
const NOTE = t => new Paragraph({ children: runs(t, { size: 19, color: "555555", italics: true }), spacing: { after: 160 } });

const border = { style: BorderStyle.SINGLE, size: 4, color: "A6A6A6" };
const borders = { top: border, bottom: border, left: border, right: border };
function table(head, rows, widths, opts = {}) {
  const cell = (t, w, isHead) => new TableCell({
    borders, width: { size: w, type: WidthType.DXA },
    shading: isHead ? { fill: "DDEEEC", type: ShadingType.CLEAR, color: "auto" } : undefined,
    margins: { top: 60, bottom: 60, left: 100, right: 100 },
    children: [new Paragraph({ children: runs(String(t), { size: opts.size || 19, bold: isHead || undefined }), spacing: { after: 0 } })],
  });
  return new Table({
    width: { size: widths.reduce((a, b) => a + b, 0), type: WidthType.DXA }, columnWidths: widths,
    rows: [new TableRow({ tableHeader: true, children: head.map((h, i) => cell(h, widths[i], true)) }),
      ...rows.map(r => new TableRow({ cantSplit: true, children: r.map((c, i) => cell(c, widths[i], false)) }))],
  });
}
const gap = () => new Paragraph({ children: [], spacing: { after: 120 } });
function img(file, widthPx, caption) {
  const buf = fs.readFileSync(file);
  // dimensões do PNG (IHDR)
  const w = buf.readUInt32BE(16), h = buf.readUInt32BE(20);
  return [
    new Paragraph({ alignment: AlignmentType.CENTER, keepNext: true, spacing: { before: 120, after: 60 },
      children: [new ImageRun({ type: "png", data: buf, transformation: { width: widthPx, height: Math.round(widthPx * h / w) },
        altText: { title: caption, description: caption, name: path.basename(file) } })] }),
    new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 200 }, children: [new TextRun({ text: caption, size: 18, italics: true })] }),
  ];
}
const D = f => path.join(ROOT, "docs", f);

// ---------- conteúdo ----------
const c = [];

// Capa
c.push(new Paragraph({ spacing: { before: 2400, after: 200 }, alignment: AlignmentType.CENTER,
  children: [new TextRun({ text: "PROJETO INTEGRADOR TRANSDISCIPLINAR", bold: true, size: 32, color: TEAL })] }));
c.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 600 },
  children: [new TextRun({ text: "ENGENHARIA DE SOFTWARE II", bold: true, size: 32, color: TEAL })] }));
c.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 200 },
  children: [new TextRun({ text: "Documentação do projeto", size: 28 })] }));
c.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { after: 1800 },
  children: [new TextRun({ text: "Aplicativo para loja de cupcakes gourmet", size: 28, bold: true })] }));
c.push(new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: "Aluno: Denis Santana Ribeiro dos Santos", size: 24 })] }));
c.push(new Paragraph({ alignment: AlignmentType.CENTER, children: [new TextRun({ text: "RGM: 33146802", size: 24 })] }));
c.push(new Paragraph({ alignment: AlignmentType.CENTER, spacing: { before: 200 }, children: [new TextRun({ text: "Versão 3 — Situações-problema 1 e 2 (planejamento e implementação)", size: 20, color: "555555" })] }));
c.push(new Paragraph({ children: [new PageBreak()] }));

// 1
c.push(H1("1. Apresentação do projeto"));
c.push(P("Este projeto especifica e desenvolve, de forma incremental, uma aplicação de vendas para uma loja de cupcakes gourmet. A iniciativa surge após uma tentativa anterior malsucedida de vender pela internet, causada por falhas no planejamento e levantamento insuficiente de requisitos. A solução facilita a consulta aos produtos e o registro de pedidos, além de apoiar a administração do catálogo e o acompanhamento das compras."));
c.push(P("Esta versão corresponde à **Situação-problema 1** do PIT de Engenharia de Software II: o projeto de Engenharia de Software I foi revisado, as inconsistências entre os artefatos foram corrigidas e o banco de dados foi modelado e documentado, preparando a implementação."));
c.push(H2("1.1 Escopo"));
c.push(B("**Incluído:** catálogo com filtro por categoria, detalhes do produto, carrinho, cadastro e login do cliente, finalização do pedido (entrega ou retirada), acompanhamento do status e área administrativa para gerenciar produtos e pedidos."));
c.push(B("**Pagamento simulado:** o cliente escolhe a forma de pagamento (PIX, dinheiro ou cartão) e paga na entrega ou na retirada. Não há integração com serviços de pagamento."));
c.push(B("**Fora do escopo:** cupom de desconto, cálculo automático de frete e publicação nas lojas de aplicativos."));

// 2
c.push(H1("2. Problema identificado"));
c.push(P("A loja tem dificuldade para vender pela internet e preservar a confiança dos clientes porque a solução anterior não foi precedida de um levantamento adequado das necessidades. Sem requisitos claros, existe o risco de construir funcionalidades inadequadas, oferecer uma experiência de compra confusa ou manter informações inconsistentes sobre produtos e pedidos."));
c.push(P("Na revisão do projeto de Engenharia de Software I, foram encontrados problemas do mesmo tipo nos próprios artefatos, corrigidos nesta versão:"));
c.push(B("O diagrama de casos de uso tinha caso de uso sem requisito (“Gerenciar usuários”), pagamento real («include» de “Processar pagamento») fora do escopo e setas nas associações entre ator e caso de uso."));
c.push(B("O diagrama de classes ligava Pedido diretamente a Produto, duplicando a classe associativa ItemPedido, e tinha atributos diferentes dos do banco de dados."));
c.push(B("O protótipo mostrava abas de categoria sem suporte no banco, um campo de cupom sem requisito e valores incorretos no carrinho."));

// 3
c.push(H1("3. Objetivos"));
c.push(H2("3.1 Objetivo geral"));
c.push(P("Planejar e desenvolver uma aplicação de vendas on-line de cupcakes gourmet, com navegação simples, informações consistentes e fluxo de compra compreensível, validando a solução progressivamente por meio de testes e avaliações de usuários."));
c.push(H2("3.2 Objetivos específicos"));
["Identificar os perfis de usuário e suas principais necessidades.",
 "Documentar requisitos funcionais e não funcionais antes da implementação.",
 "Modelar a estrutura de dados e seus relacionamentos.",
 "Construir uma interface consistente para catálogo, produto, carrinho, finalização e pedidos.",
 "Separar as responsabilidades da aplicação com a arquitetura MVC.",
 "Executar testes, registrar defeitos e realizar melhorias com base no feedback."].forEach(t => c.push(B(t)));

// 4
c.push(H1("4. Usuários e partes interessadas"));
c.push(table(["Perfil", "Necessidades principais"], [
  ["Cliente", "Consultar cupcakes por categoria, ver preço e descrição, montar o carrinho, cadastrar-se, finalizar o pedido escolhendo entrega/retirada e forma de pagamento, e acompanhar o status."],
  ["Administrador da loja", "Cadastrar, editar e desativar produtos; consultar pedidos; atualizar o status de preparação, entrega ou retirada."],
  ["Responsável pelo projeto", "Levantar requisitos, modelar, implementar, testar, documentar e manter o repositório."],
], [2400, W - 2400]));

// 5
c.push(H1("5. Decisões de escopo e tecnologia"));
c.push(table(["Decisão", "Justificativa"], [
  ["Categorias de produtos mantidas (Tradicionais, Veganos, Especiais)", "Já previstas no protótipo; facilitam a navegação. Exigiram a tabela categoria."],
  ["Cupom de desconto removido", "Não havia requisito nem regra de negócio definida; reduz o escopo da primeira versão."],
  ["Pagamento simulado (na entrega ou na retirada)", "Integração com gateway foge ao escopo do PIT; a forma de pagamento escolhida fica registrada no pedido."],
  ["Login obrigatório para finalizar e consultar pedidos", "Associa o pedido ao cliente e impede que um cliente veja pedidos de outro (RNF03). Catálogo e carrinho são públicos."],
  ["Front-end em HTML, CSS e JavaScript", "Tecnologias sugeridas pelo material; leve e responsivo para uso no celular, sem etapa de compilação."],
  ["Back-end em C# com ASP.NET Core (.NET), padrão MVC, API REST", "Opção prevista no material; o MVC separa modelos, regras e controle; tipagem forte ajuda a validar dados no servidor."],
  ["Acesso a dados com Npgsql e SQL parametrizado (repositórios)", "Única dependência externa; as consultas ficam explícitas no código e o SQL parametrizado impede injeção de SQL."],
  ["Autenticação por cookie de sessão HttpOnly", "Recurso nativo do ASP.NET Core; o JavaScript não acessa o cookie, o que reduz o risco de roubo de sessão."],
  ["Banco de dados PostgreSQL", "SGBD gratuito e robusto, com tipos NUMERIC, BOOLEAN, chaves estrangeiras e restrições CHECK que reforçam as regras de negócio."],
  ["Git e GitHub", "Versionamento e entrega do código, da documentação e das evidências."],
], [3600, W - 3600]));

// 6
c.push(H1("6. Requisitos funcionais"));
c.push(table(["ID", "Requisito", "Critério de aceitação resumido"], [
  ["RF01", "Listar produtos disponíveis", "O catálogo exibe nome, imagem e preço dos produtos ativos."],
  ["RF02", "Consultar detalhes do produto", "O cliente vê descrição, preço, categoria e disponibilidade em estoque."],
  ["RF03", "Gerenciar carrinho", "O cliente adiciona itens, altera quantidades e remove itens; subtotais e total são recalculados."],
  ["RF04", "Cadastrar-se e fazer login", "O cliente cria uma conta com nome, e-mail e senha e entra com e-mail e senha. A senha é guardada como hash."],
  ["RF05", "Finalizar pedido", "O cliente autenticado revisa os itens, escolhe entrega (com endereço) ou retirada, escolhe a forma de pagamento e confirma."],
  ["RF06", "Consultar pedidos", "O cliente vê apenas os próprios pedidos, com data, itens, total e status."],
  ["RF07", "Gerenciar produtos", "O administrador cadastra, altera e desativa produtos, informando a categoria."],
  ["RF08", "Gerenciar pedidos", "O administrador consulta pedidos e atualiza o status conforme o fluxo definido."],
  ["RF09", "Validar entradas", "Campos obrigatórios e valores inválidos geram mensagens claras e não são aceitos."],
  ["RF10", "Confirmar operação", "Após registrar o pedido, o sistema exibe a confirmação e o número do pedido."],
  ["RF11", "Filtrar catálogo por categoria", "O cliente seleciona uma categoria e vê apenas os produtos dela."],
], [800, 2500, W - 3300]));

// 7
c.push(H1("7. Requisitos não funcionais"));
c.push(table(["ID", "Categoria", "Requisito"], [
  ["RNF01", "Usabilidade", "Linguagem clara, navegação consistente e mensagens compreensíveis."],
  ["RNF02", "Segurança", "Validação de dados no servidor; funções administrativas restritas ao perfil ADMIN; senhas com hash PBKDF2; sessão em cookie HttpOnly."],
  ["RNF03", "Privacidade", "Coletar apenas os dados necessários ao pedido e não exibir dados pessoais a outros clientes."],
  ["RNF04", "Integridade", "Total do pedido calculado no servidor a partir dos preços do banco, sem confiar em valores enviados pela interface."],
  ["RNF05", "Compatibilidade", "Interface responsiva, utilizável em celulares e computadores."],
  ["RNF06", "Manutenibilidade", "Código organizado em MVC, documentação atualizada e controle de versão no GitHub."],
  ["RNF07", "Persistência", "Pedidos confirmados armazenados no PostgreSQL."],
], [900, 1900, W - 2800]));

// 8
c.push(H1("8. Histórias de usuário"));
["Como cliente, quero visualizar o catálogo para escolher os cupcakes disponíveis.",
 "Como cliente, quero filtrar o catálogo por categoria para encontrar mais rápido o tipo de cupcake que procuro.",
 "Como cliente, quero ver os detalhes e o preço de um cupcake para decidir se desejo comprá-lo.",
 "Como cliente, quero alterar as quantidades no carrinho para revisar meu pedido antes de confirmar.",
 "Como cliente, quero escolher entre entrega e retirada e a forma de pagamento para receber o pedido do jeito que prefiro.",
 "Como cliente, quero receber uma confirmação após finalizar o pedido para saber que ele foi registrado.",
 "Como cliente, quero acompanhar o status do pedido para saber em que etapa ele está.",
 "Como administrador, quero cadastrar e atualizar produtos para manter o catálogo correto.",
 "Como administrador, quero atualizar o status dos pedidos para informar o andamento da compra."].forEach(t => c.push(B(t)));

// 9
c.push(H1("9. Metodologia e organização do trabalho"));
c.push(P("O trabalho segue desenvolvimento incremental com práticas ágeis inspiradas no Scrum: backlog priorizado, divisão em incrementos e revisão frequente do que foi produzido. Como o projeto é individual e sem sprints formais com equipe, ele é caracterizado como desenvolvimento incremental com práticas ágeis, e não como Scrum completo."));
c.push(table(["Atividade (Situação-problema 1)", "Tempo", "Evidência"], [
  ["Análise do projeto anterior e levantamento das inconsistências", "1 dia", "Lista de correções (seção 2)"],
  ["Definição de escopo e da tecnologia", "1 dia", "Decisões (seção 5)"],
  ["Revisão dos diagramas de casos de uso e de classes", "2 dias", "Diagramas (seção 11)"],
  ["Modelagem do banco, script físico e dicionário de dados", "2 dias", "DER, script e dicionário (seções 12 e 13)"],
  ["Revisão do protótipo das telas", "1 dia", "Protótipo (seção 15)"],
  ["Atualização da documentação", "1 dia", "Este documento"],
  ["**Total**", "**8 dias**", ""],
], [4600, 1100, W - 5700]));

// 10
c.push(H1("10. Arquitetura da solução"));
c.push(P("A aplicação é dividida em três camadas, seguindo o padrão MVC no back-end:"));
c.push(B("**Front-end (visão):** páginas em HTML, CSS e JavaScript que consomem a API por requisições HTTP (fetch). O carrinho fica no navegador até a finalização."));
c.push(B("**Back-end (controle e modelo):** API REST em ASP.NET Core. Os controladores recebem as requisições, os serviços aplicam as regras de negócio (cálculo do total, baixa de estoque, fluxo de status) e os modelos representam as entidades do diagrama de classes. O acesso ao banco usa o driver Npgsql com SQL parametrizado, organizado em classes de repositório (camada de dados)."));
c.push(B("**Banco de dados:** PostgreSQL, com as restrições descritas nas seções 12 e 13 como segunda linha de defesa das regras de negócio."));
c.push(P("A autenticação usa um cookie de sessão HttpOnly (inacessível ao JavaScript, SameSite=Lax), criado no login e válido por 8 horas. As rotas administrativas exigem o perfil ADMIN; as senhas são guardadas com hash PBKDF2 do ASP.NET Core Identity."));

// 11
c.push(H1("11. Diagramas UML"));
c.push(H2("11.1 Diagrama de casos de uso"));
c.push(P("Atores: Cliente e Administrador, fora da fronteira do sistema. As associações entre ator e caso de uso são linhas simples. “Filtrar por categoria” estende “Visualizar catálogo” («extend»), pois é opcional. “Finalizar pedido” inclui “Escolher entrega/retirada e forma de pagamento” («include»), pois essa etapa sempre acontece. Finalizar e acompanhar pedidos têm como pré-condição o cliente estar autenticado."));
c.push(...img(D("diagramas/casos-de-uso.png"), 600, "Figura 1 – Diagrama de casos de uso"));
c.push(H2("11.2 Diagrama de classes"));
c.push(P("Classes: Usuario, Categoria, Produto, Pedido e ItemPedido, mais quatro enumerações. Relações: Usuario 1 — 0..* Pedido; Categoria 1 — 0..* Produto; Pedido 1 ◆— 1..* ItemPedido (composição: os itens não existem sem o pedido); Produto 1 — 0..* ItemPedido. ItemPedido funciona como classe associativa entre Pedido e Produto e guarda a quantidade e o preço unitário do momento da compra. A ligação direta Pedido–Produto foi removida. Valores em dinheiro usam decimal."));
c.push(...img(D("diagramas/classes.png"), 600, "Figura 2 – Diagrama de classes"));

// 12
c.push(H1("12. Modelo de dados"));
c.push(P("O modelo lógico tem cinco tabelas. Os nomes seguem o padrão do PostgreSQL (minúsculas com sublinhado). O script físico está no repositório em database/01_schema.sql, e a carga inicial em database/02_seed.sql."));
c.push(...img(D("diagramas/der.png"), 470, "Figura 3 – Modelo lógico (DER)"));
c.push(table(["Tabela", "Regras principais"], [
  ["usuario", "E-mail único e em minúsculas; senha guardada apenas como hash; perfil CLIENTE ou ADMIN."],
  ["categoria", "Nome único; ordem de exibição define a sequência das abas no catálogo."],
  ["produto", "Pertence a uma categoria; preço e estoque não negativos; produto inativo não aparece para compra."],
  ["pedido", "Pertence a um usuário; status, tipo de entrega e forma de pagamento restritos a valores definidos; endereço obrigatório quando o tipo é ENTREGA; status compatível com o tipo de entrega."],
  ["item_pedido", "Quantidade maior que zero; um produto aparece no máximo uma vez por pedido; preço unitário copiado no momento da compra; subtotal calculado pelo banco."],
], [1700, W - 1700]));
c.push(gap());
c.push(H2("12.1 Fluxo de status do pedido"));
c.push(table(["Status", "Significado", "Próximo status permitido"], [
  ["RECEBIDO", "Pedido registrado pelo cliente", "EM_PREPARO ou CANCELADO"],
  ["EM_PREPARO", "Pedido em produção na loja", "SAIU_PARA_ENTREGA (entrega), PRONTO_PARA_RETIRADA (retirada) ou CANCELADO"],
  ["PRONTO_PARA_RETIRADA", "Aguardando o cliente na loja", "ENTREGUE"],
  ["SAIU_PARA_ENTREGA", "A caminho do endereço informado", "ENTREGUE"],
  ["ENTREGUE", "Pedido concluído e pago", "—"],
  ["CANCELADO", "Pedido cancelado pela loja", "—"],
], [2600, 2900, W - 5500]));
c.push(NOTE("A compatibilidade entre status e tipo de entrega é garantida no banco (restrição ck_pedido_status_tipo); a sequência de transições será validada no back-end."));

// 13
c.push(H1("13. Dicionário de dados"));
const dic = [
  ["usuario.id_usuario", "INT identity, PK", "Sim", "Identificador do usuário, gerado automaticamente."],
  ["usuario.nome", "VARCHAR(120)", "Sim", "Nome do cliente ou administrador; mínimo de 2 caracteres."],
  ["usuario.email", "VARCHAR(180), UNIQUE", "Sim", "E-mail de login; único e em letras minúsculas."],
  ["usuario.telefone", "VARCHAR(20)", "Não", "Telefone de contato."],
  ["usuario.senha_hash", "VARCHAR(255)", "Sim", "Hash PBKDF2 (ASP.NET Core Identity) da senha; a senha original nunca é armazenada."],
  ["usuario.perfil", "VARCHAR(20)", "Sim", "CLIENTE (padrão) ou ADMIN."],
  ["usuario.data_cadastro", "TIMESTAMPTZ", "Sim", "Data e hora do cadastro, com fuso (padrão: agora)."],
  ["categoria.id_categoria", "INT identity, PK", "Sim", "Identificador da categoria."],
  ["categoria.nome", "VARCHAR(60), UNIQUE", "Sim", "Nome exibido na aba do catálogo."],
  ["categoria.descricao", "VARCHAR(255)", "Não", "Descrição curta da categoria."],
  ["categoria.ordem_exibicao", "INT", "Sim", "Posição da categoria no catálogo (padrão 0)."],
  ["categoria.ativo", "BOOLEAN", "Sim", "Indica se a categoria aparece no catálogo (padrão verdadeiro)."],
  ["produto.id_produto", "INT identity, PK", "Sim", "Identificador do produto."],
  ["produto.id_categoria", "INT, FK → categoria", "Sim", "Categoria do produto; não permite excluir categoria em uso."],
  ["produto.nome", "VARCHAR(120)", "Sim", "Nome do cupcake."],
  ["produto.descricao", "TEXT", "Não", "Descrição, ingredientes ou observações."],
  ["produto.preco", "NUMERIC(10,2)", "Sim", "Preço atual; maior ou igual a zero."],
  ["produto.quantidade_estoque", "INT", "Sim", "Unidades disponíveis; não pode ser negativa (padrão 0)."],
  ["produto.imagem_url", "VARCHAR(255)", "Não", "Caminho ou URL da imagem do produto."],
  ["produto.ativo", "BOOLEAN", "Sim", "Se falso, o produto não aparece para compra (padrão verdadeiro)."],
  ["produto.data_cadastro", "TIMESTAMPTZ", "Sim", "Data e hora do cadastro, com fuso (padrão: agora)."],
  ["pedido.id_pedido", "INT identity, PK", "Sim", "Número do pedido exibido ao cliente."],
  ["pedido.id_usuario", "INT, FK → usuario", "Sim", "Cliente que fez o pedido."],
  ["pedido.data_pedido", "TIMESTAMPTZ", "Sim", "Data e hora do registro, com fuso (padrão: agora)."],
  ["pedido.data_atualizacao", "TIMESTAMPTZ", "Sim", "Data e hora da última mudança de status."],
  ["pedido.status", "VARCHAR(30)", "Sim", "RECEBIDO (padrão), EM_PREPARO, PRONTO_PARA_RETIRADA, SAIU_PARA_ENTREGA, ENTREGUE ou CANCELADO."],
  ["pedido.valor_total", "NUMERIC(10,2)", "Sim", "Soma dos subtotais, calculada pelo servidor; maior ou igual a zero."],
  ["pedido.tipo_entrega", "VARCHAR(20)", "Sim", "ENTREGA ou RETIRADA."],
  ["pedido.endereco_entrega", "VARCHAR(255)", "Condicional", "Obrigatório (mínimo de 5 caracteres) quando o tipo é ENTREGA."],
  ["pedido.forma_pagamento", "VARCHAR(20)", "Sim", "DINHEIRO, PIX ou CARTAO — pago na entrega ou na retirada (simulado)."],
  ["pedido.observacao", "VARCHAR(255)", "Não", "Observação do cliente (ex.: ponto de referência)."],
  ["item_pedido.id_item", "INT identity, PK", "Sim", "Identificador do item."],
  ["item_pedido.id_pedido", "INT, FK → pedido", "Sim", "Pedido do item; itens são excluídos junto com o pedido."],
  ["item_pedido.id_produto", "INT, FK → produto", "Sim", "Produto comprado; não permite excluir produto já vendido."],
  ["item_pedido.quantidade", "INT", "Sim", "Quantidade comprada; maior que zero."],
  ["item_pedido.preco_unitario", "NUMERIC(10,2)", "Sim", "Preço do produto no momento da compra."],
  ["item_pedido.subtotal", "NUMERIC(10,2), calculado", "Automático", "quantidade × preco_unitario, gerado pelo banco."],
];
c.push(table(["Tabela.campo", "Tipo", "Obrigatório", "Descrição / regra"], dic, [2600, 2000, 1150, W - 5750], { size: 17 }));

// 14
c.push(H1("14. Validação do script do banco"));
c.push(P("O script físico e a carga inicial foram executados em um servidor PostgreSQL 16. Em seguida, foram feitas tentativas de gravar dados inválidos para conferir as restrições. Todas foram rejeitadas pelo banco:"));
c.push(table(["#", "Tentativa", "Resultado"], [
  ["1", "Pedido de entrega sem endereço", "Rejeitado (ck_pedido_endereco)"],
  ["2", "Pedido de retirada sem endereço", "Aceito, como esperado"],
  ["3", "Pedido de retirada com status SAIU_PARA_ENTREGA", "Rejeitado (ck_pedido_status_tipo)"],
  ["4", "Item com quantidade zero", "Rejeitado (ck_item_quantidade)"],
  ["5", "Produto com preço negativo", "Rejeitado (ck_produto_preco)"],
  ["6", "Usuário com e-mail já cadastrado", "Rejeitado (uq_usuario_email)"],
  ["7", "Usuário com perfil inexistente", "Rejeitado (ck_usuario_perfil)"],
  ["8", "Pedido com forma de pagamento BOLETO", "Rejeitado (ck_pedido_pagto)"],
  ["9", "Subtotal informado manualmente", "Rejeitado (coluna calculada)"],
  ["10", "Excluir produto que já foi vendido", "Rejeitado (fk_item_pedido_produto)"],
  ["11", "Estoque negativo", "Rejeitado (ck_produto_estoque)"],
  ["12", "E-mail com letras maiúsculas", "Rejeitado (ck_usuario_email)"],
], [500, 4700, W - 5200]));
c.push(NOTE("Também foi conferido que o subtotal é calculado corretamente: 2 × R$ 12,00 = R$ 24,00 e 1 × R$ 12,00 = R$ 12,00."));

// 15
c.push(H1("15. Protótipo das telas"));
c.push(P("O protótipo foi refeito para ficar coerente com os requisitos e o banco: abas de categoria, indicação de disponibilidade, carrinho com contas corretas e sem cupom, tela de finalização com entrega/retirada e forma de pagamento, acompanhamento do status e painel do administrador. Mensagens de erro indicam o que corrigir; mensagens de sucesso informam o número do pedido."));
c.push(...img(D("prototipo/prototipo-telas-1.png"), 600, "Figura 4 – Catálogo, detalhes do produto e carrinho"));
c.push(...img(D("prototipo/prototipo-telas-2.png"), 600, "Figura 5 – Finalização, status do pedido e administração"));
c.push(table(["Tela", "Elementos e comportamento", "Requisitos"], [
  ["Catálogo", "Abas de categoria, cartões com imagem, nome, preço e botão de adicionar; produtos sem estoque marcados como esgotados.", "RF01, RF11"],
  ["Detalhes do produto", "Imagem, categoria, descrição, preço, disponibilidade, seletor de quantidade e botão de adicionar.", "RF02, RF03"],
  ["Carrinho", "Itens com preço unitário, quantidade editável, subtotal, remoção e total.", "RF03"],
  ["Finalização", "Entrega ou retirada, endereço (obrigatório na entrega), forma de pagamento, observação, resumo e confirmação.", "RF04, RF05, RF09"],
  ["Status do pedido", "Confirmação com número, linha do tempo do status e resumo do valor a pagar.", "RF06, RF10"],
  ["Administração", "Pedidos abertos com status e alteração de status; aba de produtos para cadastro e edição.", "RF07, RF08"],
], [1900, W - 3300, 1400]));

// 16
c.push(H1("16. Plano e resultado dos testes"));
c.push(P("Os casos foram executados em 21/10/2026 de duas formas: **automatizada** (xUnit, aplicação e PostgreSQL reais, comando dotnet test) e **no navegador** (Playwright, percorrendo as telas como um usuário). Todos foram aprovados."));
c.push(table(["ID", "Cenário", "Resultado esperado", "Execução", "Resultado"], [
  ["CT01", "Abrir catálogo com produtos ativos", "Os produtos ativos aparecem com nome, imagem e preço.", "xUnit + navegador", "Aprovado"],
  ["CT02", "Filtrar por uma categoria", "Somente os produtos daquela categoria são exibidos.", "xUnit + navegador", "Aprovado"],
  ["CT03", "Consultar produto inativo ou inexistente", "O sistema informa que o produto não está disponível.", "xUnit + navegador", "Aprovado"],
  ["CT04", "Adicionar produto ao carrinho", "O item aparece com quantidade e subtotal corretos.", "Navegador", "Aprovado"],
  ["CT05", "Alterar quantidade para zero ou valor inválido", "O sistema impede o valor inválido; remover exige ação explícita.", "xUnit + navegador", "Aprovado"],
  ["CT06", "Finalizar pedido sem estar logado", "O sistema pede login antes de continuar.", "xUnit + navegador", "Aprovado"],
  ["CT07", "Finalizar entrega sem endereço", "O sistema indica o campo obrigatório e não registra o pedido.", "xUnit + navegador", "Aprovado"],
  ["CT08", "Confirmar pedido válido (entrega ou retirada)", "O pedido é gravado, o estoque é baixado e o número do pedido é exibido.", "xUnit + navegador", "Aprovado"],
  ["CT09", "Pedir quantidade maior que o estoque (inclusive pedidos simultâneos)", "O sistema informa a quantidade disponível e não registra o pedido.", "xUnit", "Aprovado"],
  ["CT10", "Consultar pedido de outro cliente", "Acesso negado.", "xUnit", "Aprovado"],
  ["CT11", "Cliente tenta acessar funções de administrador", "Acesso negado.", "xUnit", "Aprovado"],
  ["CT12", "Administrador altera o status do pedido (e cancela)", "O novo status é salvo e aparece para o cliente; cancelamento devolve o estoque.", "xUnit + navegador", "Aprovado"],
  ["CT13", "Enviar preço ou total alterado pela interface", "O servidor recalcula o total com os preços do banco.", "xUnit", "Aprovado"],
  ["CT14", "Cadastrar-se com e-mail já usado", "O sistema informa que o e-mail já está cadastrado.", "xUnit + navegador", "Aprovado"],
], [700, 2350, 3226, 1600, 1150]));
c.push(gap());
c.push(P("Resumo da execução automatizada: **47 testes, 47 aprovados** — 25 unitários (cálculo do total, preço copiado no momento da compra, baixa de estoque, transições de status, validações do pedido, hash de senha) e 22 de integração (todos os casos acima, além de login/logout, validação de cadastro, gestão de produtos e páginas do front-end)."));
c.push(H2("16.1 Defeitos encontrados e corrigidos durante o desenvolvimento"));
c.push(table(["#", "Defeito", "Como foi encontrado", "Correção"], [
  ["1", "Login e cadastro retornavam erro 500: as regras de validação estavam em records, formato não aceito pelo ASP.NET.", "Teste manual da API", "Modelos de entrada convertidos em classes com atributos de validação."],
  ["2", "Mensagens de erro com acentos apareciam codificadas (\\u00E7).", "Teste manual da API", "Serialização JSON configurada para manter os acentos."],
  ["3", "Janela de cadastro de produto abria no canto da tela.", "Teste no navegador (captura de tela)", "Centralização da janela restaurada no CSS."],
  ["4", "Quando a API era iniciada pelos testes, os endereços da API respondiam 404.", "Testes automatizados", "Registro explícito do assembly dos controladores."],
], [400, 3426, 2300, 2900]));

// 17
c.push(H1("17. Implementação (Situação-problema 2)"));
c.push(H2("17.1 Estrutura do repositório"));
c.push(table(["Pasta / arquivo", "Conteúdo"], [
  ["database/", "01_schema.sql (tabelas e restrições) e 02_seed.sql (carga inicial)."],
  ["src/Cupcakes.Api/Models", "Entidades e regras de negócio (Usuario, Categoria, Produto, Pedido, ItemPedido, FluxoStatus)."],
  ["src/Cupcakes.Api/Data", "Repositórios com SQL parametrizado e criação automática do banco."],
  ["src/Cupcakes.Api/Services", "Serviços: pedidos (transação, estoque, total), senhas e mapeamento para DTOs."],
  ["src/Cupcakes.Api/Controllers", "Controladores MVC da API REST: autenticação, catálogo, pedidos e administração."],
  ["src/Cupcakes.Api/wwwroot", "Front-end: páginas HTML, CSS, JavaScript e ilustrações dos produtos."],
  ["tests/Cupcakes.Tests", "Testes unitários e de integração (xUnit)."],
  ["docs/", "Diagramas, protótipo e capturas de tela."],
], [2800, W - 2800]));
c.push(gap());
c.push(H2("17.2 Endereços da API"));
c.push(table(["Método e rota", "Acesso", "Função"], [
  ["POST /api/auth/cadastro", "Público", "Cria a conta do cliente e inicia a sessão (RF04)."],
  ["POST /api/auth/login · POST /api/auth/logout", "Público", "Entra e sai (cookie de sessão)."],
  ["GET /api/auth/me", "Logado", "Dados do usuário conectado."],
  ["GET /api/categorias", "Público", "Categorias do catálogo (RF11)."],
  ["GET /api/produtos[?categoriaId=]", "Público", "Produtos ativos, com filtro opcional (RF01, RF11)."],
  ["GET /api/produtos/{id}", "Público", "Detalhes do produto (RF02)."],
  ["POST /api/pedidos", "Cliente", "Finaliza o pedido; total calculado no servidor (RF05, RF10)."],
  ["GET /api/pedidos · GET /api/pedidos/{id}", "Cliente", "Pedidos do próprio cliente e status (RF06)."],
  ["GET /api/admin/pedidos[?situacao=]", "Admin", "Pedidos abertos, concluídos ou todos (RF08)."],
  ["PATCH /api/admin/pedidos/{id}/status", "Admin", "Avança ou cancela o pedido conforme o fluxo (RF08)."],
  ["GET/POST/PUT /api/admin/produtos", "Admin", "Lista, cadastra e altera produtos (RF07)."],
  ["PATCH /api/admin/produtos/{id}/ativo", "Admin", "Ativa ou desativa um produto (RF07)."],
], [3500, 1100, W - 4600]));
c.push(gap());
c.push(H2("17.3 Como executar"));
c.push(N("Instale o .NET 8 SDK e o PostgreSQL.", "num3"));
c.push(N("Crie um banco vazio chamado cupcakes e ajuste a conexão em src/Cupcakes.Api/appsettings.json (ConnectionStrings:Cupcakes).", "num3"));
c.push(N("Na pasta do projeto, execute: dotnet run --project src/Cupcakes.Api. Na primeira execução, as tabelas e a carga inicial são criadas automaticamente.", "num3"));
c.push(N("Abra http://localhost:5000 no navegador. Administrador: admin@cupcakes.com / Admin@123. Cliente: cliente@teste.com / Cliente@123.", "num3"));
c.push(N("Para os testes automatizados: crie o banco cupcakes_teste (ou defina a variável CUPCAKES_TEST_DB) e execute dotnet test.", "num3"));
c.push(H2("17.4 Telas da aplicação"));
c.push(...img(D("telas/composto-cliente-1.png"), 600, "Figura 6 – Catálogo, detalhes, carrinho e validação na finalização"));
c.push(...img(D("telas/composto-cliente-2.png"), 600, "Figura 7 – Pedido confirmado, meus pedidos, status atualizado e validação do cadastro"));
c.push(...img(D("telas/10-admin-pedidos.png"), 600, "Figura 8 – Painel do administrador: pedidos em aberto"));
c.push(...img(D("telas/12-admin-produtos.png"), 600, "Figura 9 – Painel do administrador: produtos"));

// 18
c.push(H1("18. Resultado e próximos passos"));
c.push(P("As Situações-problema 1 e 2 foram concluídas: a documentação está coerente com o código, a aplicação implementa todos os requisitos funcionais (RF01 a RF11) e os 14 casos de teste foram aprovados. O próximo passo é:"));
c.push(N("Situação-problema 3: publicar a aplicação, pedir a cinco pessoas que a avaliem, registrar erros e melhorias, gerar o PDF das avaliações e gravar o vídeo de demonstração.", "num2"));

// 19
c.push(H1("19. Referências"));
c.push(P("MATERIAL DIDÁTICO DA DISCIPLINA. Projeto Integrador Transdisciplinar em Engenharia de Software II. Disponível no ambiente virtual de aprendizagem."));
c.push(P("POSTGRESQL GLOBAL DEVELOPMENT GROUP. PostgreSQL Documentation. Disponível em: https://www.postgresql.org/docs/."));
c.push(P("MICROSOFT. ASP.NET Core documentation. Disponível em: https://learn.microsoft.com/aspnet/core/."));

// ---------- documento ----------
const doc = new Document({
  creator: "Denis Santana Ribeiro dos Santos",
  title: "Documentação — Aplicativo para loja de cupcakes gourmet",
  styles: {
    default: { document: { run: { font: "Arial", size: 21 } } },
    paragraphStyles: [
      { id: "Heading1", name: "Heading 1", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 28, bold: true, color: TEAL, font: "Arial" }, paragraph: { spacing: { before: 360, after: 160 }, outlineLevel: 0, keepNext: true } },
      { id: "Heading2", name: "Heading 2", basedOn: "Normal", next: "Normal", quickFormat: true,
        run: { size: 23, bold: true, color: "333333", font: "Arial" }, paragraph: { spacing: { before: 240, after: 120 }, outlineLevel: 1, keepNext: true } },
    ],
  },
  numbering: { config: [
    { reference: "bul", levels: [{ level: 0, format: LevelFormat.BULLET, text: "•", alignment: AlignmentType.LEFT,
      style: { paragraph: { indent: { left: 567, hanging: 283 } } } }] },
    { reference: "num", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT,
      style: { paragraph: { indent: { left: 567, hanging: 340 } } } }] },
    { reference: "num3", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT,
      style: { paragraph: { indent: { left: 567, hanging: 340 } } } }] },
    { reference: "num2", levels: [{ level: 0, format: LevelFormat.DECIMAL, text: "%1.", alignment: AlignmentType.LEFT,
      style: { paragraph: { indent: { left: 567, hanging: 340 } } } }] },
  ] },
  sections: [{
    properties: { page: { size: { width: 11906, height: 16838 }, margin: { top: 1418, bottom: 1418, left: 1440, right: 1440 } } },
    footers: { default: new Footer({ children: [new Paragraph({ alignment: AlignmentType.RIGHT,
      children: [new TextRun({ text: "PIT Engenharia de Software II — Cupcakes Gourmet   |   ", size: 16, color: "777777" }),
                 new TextRun({ children: [PageNumber.CURRENT], size: 16, color: "777777" })] })] }) },
    children: c,
  }],
});
Packer.toBuffer(doc).then(b => { fs.mkdirSync(path.dirname(OUT), { recursive: true }); fs.writeFileSync(OUT, b); console.log("ok", OUT); });
