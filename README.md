# Cupcakes Gourmet — PIT Engenharia de Software II

Aplicação web de vendas para uma loja de cupcakes gourmet, desenvolvida no Projeto Integrador Transdisciplinar em Engenharia de Software II.

**Aluno:** Denis Santana Ribeiro dos Santos — RGM 33146802

## Funcionalidades

**Cliente**
- Catálogo com filtro por categoria (Tradicionais, Veganos, Especiais) e indicação de produtos esgotados
- Detalhes do produto com seletor de quantidade limitado ao estoque
- Carrinho com alteração de quantidades, remoção e total
- Cadastro, login e logout
- Finalização com **entrega** (endereço obrigatório) ou **retirada na loja**
- Pagamento **simulado**: PIX, dinheiro ou cartão, pago na entrega ou na retirada
- Confirmação com número do pedido, lista de pedidos e linha do tempo do status

**Administrador**
- Pedidos abertos, concluídos ou todos; avanço de status conforme o fluxo e cancelamento (devolve o estoque)
- Cadastro, edição, ativação e desativação de produtos

**Regras garantidas no servidor**
- Total e subtotais calculados com os preços do banco; valores enviados pela interface são ignorados
- Estoque conferido e baixado dentro de uma transação, com bloqueio de linha (pedidos simultâneos não vendem além do estoque)
- Cliente só vê os próprios pedidos; rotas administrativas exigem o perfil ADMIN
- Senhas com hash PBKDF2; sessão em cookie HttpOnly

## Tecnologias

| Camada | Tecnologia |
|---|---|
| Front-end | HTML, CSS e JavaScript (sem frameworks), responsivo |
| Back-end | C# / ASP.NET Core 8 — padrão MVC, API REST |
| Acesso a dados | Npgsql com SQL parametrizado (repositórios) |
| Banco de dados | PostgreSQL |
| Testes | xUnit (unitários e de integração) |

## Como executar

Pré-requisitos: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) e [PostgreSQL](https://www.postgresql.org/download/) 14 ou superior.

1. Crie um banco vazio:
   ```bash
   createdb -U postgres cupcakes
   ```
2. Ajuste usuário e senha do PostgreSQL em `src/Cupcakes.Api/appsettings.json`:
   ```json
   "ConnectionStrings": {
     "Cupcakes": "Host=localhost;Port=5432;Database=cupcakes;Username=postgres;Password=postgres"
   }
   ```
3. Execute:
   ```bash
   dotnet run --project src/Cupcakes.Api
   ```
   Na primeira execução, as tabelas e a carga inicial (`database/*.sql`) são criadas automaticamente.
4. Abra **http://localhost:5000**.

Usuários de demonstração (troque as senhas antes de publicar):

| Perfil | E-mail | Senha |
|---|---|---|
| Administrador | admin@cupcakes.com | Admin@123 |
| Cliente | cliente@teste.com | Cliente@123 |

O painel do administrador fica em **/admin.html** (ou em Perfil → Painel do administrador).

> Para criar o banco manualmente em vez da criação automática: execute `database/01_schema.sql` e depois `database/02_seed.sql`, e defina `"Banco": { "InicializarAutomaticamente": false }`.

## Testes automatizados

Os testes de integração sobem a aplicação real e usam um banco PostgreSQL próprio (`cupcakes_teste`), recriado a cada execução.

```bash
createdb -U postgres cupcakes_teste
dotnet test
```

Se a conexão for diferente do padrão (`localhost`, usuário `postgres`, senha `postgres`), defina a variável de ambiente:

```bash
# Linux/macOS
export CUPCAKES_TEST_DB="Host=localhost;Port=5432;Username=postgres;Password=SUA_SENHA;Database=cupcakes_teste"
# Windows (PowerShell)
$env:CUPCAKES_TEST_DB="Host=localhost;Port=5432;Username=postgres;Password=SUA_SENHA;Database=cupcakes_teste"
```

Resultado da última execução: **47 testes, 47 aprovados** (casos CT01 a CT14 do plano de testes, regras de negócio, login, validações e páginas do front-end).

## Publicação (opcional)

O `Dockerfile` permite publicar em serviços como Render ou Railway:

- Crie um banco PostgreSQL no serviço (ou no Neon/Supabase) e defina a variável **`DATABASE_URL`** no formato `postgres://usuario:senha@host:5432/banco`.
- A porta é lida da variável **`PORT`** (padrão 8080).
- Na primeira inicialização, as tabelas e a carga inicial são criadas automaticamente.

## Estrutura

```
database/                 scripts SQL (tabelas, restrições e carga inicial)
src/Cupcakes.Api/
  Models/                 entidades e regras de negócio (Pedido, Produto, FluxoStatus...)
  Data/                   repositórios (SQL) e criação automática do banco
  Services/               regras dos pedidos, senhas e conversão para DTOs
  Controllers/            controladores MVC da API REST
  Dtos/                   modelos de entrada (com validação) e de saída
  Infra/                  tratamento de erros e leitura da conexão
  wwwroot/                front-end (HTML, CSS, JS e imagens)
tests/Cupcakes.Tests/     testes unitários e de integração
docs/                     diagramas UML, DER, protótipo e capturas de tela
entregas/                 documentos entregues na disciplina
tools/                    scripts que geram diagramas, imagens e documentos
```

## Endereços da API

| Método e rota | Acesso | Função |
|---|---|---|
| `POST /api/auth/cadastro` | Público | Cria a conta do cliente e inicia a sessão |
| `POST /api/auth/login`, `POST /api/auth/logout` | Público | Entra e sai |
| `GET /api/auth/me` | Logado | Usuário conectado |
| `GET /api/categorias` | Público | Categorias do catálogo |
| `GET /api/produtos?categoriaId=` | Público | Produtos ativos (filtro opcional) |
| `GET /api/produtos/{id}` | Público | Detalhes do produto |
| `POST /api/pedidos` | Cliente | Finaliza o pedido |
| `GET /api/pedidos`, `GET /api/pedidos/{id}` | Cliente | Pedidos do cliente e status |
| `GET /api/admin/pedidos?situacao=abertos\|concluidos` | Admin | Pedidos |
| `PATCH /api/admin/pedidos/{id}/status` | Admin | Muda o status |
| `GET/POST/PUT /api/admin/produtos` | Admin | Produtos |
| `PATCH /api/admin/produtos/{id}/ativo` | Admin | Ativa/desativa produto |

## Andamento

- [x] Situação-problema 1 — revisão da documentação, diagramas, protótipo e banco de dados
- [x] Situação-problema 2 — implementação (front-end, back-end, banco e testes)
- [ ] Situação-problema 3 — avaliação por cinco pessoas, melhorias e vídeo
