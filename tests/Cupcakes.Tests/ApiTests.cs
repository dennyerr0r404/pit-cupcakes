using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Cupcakes.Tests;

/// <summary>
/// Testes de integração: aplicação + PostgreSQL reais, pela API HTTP.
/// Os nomes CTxx correspondem ao plano de testes da documentação (seção 16).
/// </summary>
public class ApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private record Categoria(int Id, string Nome);
    private record Produto(int Id, string Nome, decimal Preco, int QuantidadeEstoque, bool Disponivel, int CategoriaId, string Categoria, bool Ativo);
    private record Item(int ProdutoId, string Produto, int Quantidade, decimal PrecoUnitario, decimal Subtotal);
    private record Etapa(string Status, string Rotulo, bool Concluida, bool Atual);
    private record Pedido(int Id, string Status, decimal ValorTotal, string TipoEntrega, string? EnderecoEntrega,
        string FormaPagamento, List<Item> Itens, List<Etapa> Etapas, List<string> ProximosStatus);
    private record Erro(string Mensagem);

    private static async Task<T> Ler<T>(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<T>(Json))!;

    private static object PedidoEntrega(params (int id, int qtd)[] itens) => new
    {
        tipoEntrega = "ENTREGA",
        enderecoEntrega = "Rua das Flores, 100",
        formaPagamento = "PIX",
        itens = itens.Select(i => new { produtoId = i.id, quantidade = i.qtd }),
    };

    private async Task<Produto> CriarProdutoAsync(HttpClient admin, string nome, decimal preco, int estoque, int categoriaId = 1)
    {
        var r = await admin.PostAsJsonAsync("/api/admin/produtos",
            new { nome, preco, quantidadeEstoque = estoque, categoriaId, descricao = "Teste", ativo = true });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return await Ler<Produto>(r);
    }

    // ------------------------------------------------------------- Catálogo

    [Fact(DisplayName = "CT01 - Catálogo lista os produtos ativos com nome e preço")]
    public async Task CT01_CatalogoListaProdutosAtivos()
    {
        var produtos = await api.NovoCliente().GetFromJsonAsync<List<Produto>>("/api/produtos", Json);
        Assert.NotNull(produtos);
        Assert.True(produtos!.Count >= 7);
        Assert.All(produtos, p => { Assert.True(p.Ativo); Assert.False(string.IsNullOrEmpty(p.Nome)); Assert.True(p.Preco > 0); });
        Assert.Contains(produtos, p => p.Nome == "Velvet Red" && p.Preco == 12.00m);
    }

    [Fact(DisplayName = "CT02 - Filtrar por categoria mostra só os produtos dela")]
    public async Task CT02_FiltroPorCategoria()
    {
        var http = api.NovoCliente();
        var categorias = await http.GetFromJsonAsync<List<Categoria>>("/api/categorias", Json);
        var veganos = categorias!.Single(c => c.Nome == "Veganos");

        var produtos = await http.GetFromJsonAsync<List<Produto>>($"/api/produtos?categoriaId={veganos.Id}", Json);
        Assert.NotEmpty(produtos!);
        Assert.All(produtos!, p => Assert.Equal("Veganos", p.Categoria));
    }

    [Fact(DisplayName = "CT03 - Produto inexistente ou inativo não é exibido")]
    public async Task CT03_ProdutoInexistenteOuInativo()
    {
        var http = api.NovoCliente();
        var r = await http.GetAsync("/api/produtos/99999");
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        Assert.Contains("não está disponível", (await Ler<Erro>(r)).Mensagem);

        var admin = await api.AdminAsync();
        var p = await CriarProdutoAsync(admin, "Produto Temporário", 9m, 5);
        await admin.PatchAsJsonAsync($"/api/admin/produtos/{p.Id}/ativo", new { ativo = false });

        Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/produtos/{p.Id}")).StatusCode);
        var catalogo = await http.GetFromJsonAsync<List<Produto>>("/api/produtos", Json);
        Assert.DoesNotContain(catalogo!, x => x.Id == p.Id);
    }

    // ------------------------------------------------------------- Pedido

    [Fact(DisplayName = "CT04/CT08 - Pedido válido calcula subtotais e total e baixa o estoque")]
    public async Task CT08_PedidoValidoCalculaTotalEBaixaEstoque()
    {
        var admin = await api.AdminAsync();
        var a = await CriarProdutoAsync(admin, "Teste CT08 A", 12m, 10);
        var b = await CriarProdutoAsync(admin, "Teste CT08 B", 12m, 10);
        var cliente = await api.NovoClienteCadastradoAsync();

        var r = await cliente.PostAsJsonAsync("/api/pedidos", PedidoEntrega((a.Id, 2), (b.Id, 1)));
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var pedido = await Ler<Pedido>(r);

        Assert.Equal("RECEBIDO", pedido.Status);
        Assert.Equal(36.00m, pedido.ValorTotal);
        Assert.Equal(24.00m, pedido.Itens.Single(i => i.ProdutoId == a.Id).Subtotal);
        Assert.Equal(8, (await cliente.GetFromJsonAsync<Produto>($"/api/produtos/{a.Id}", Json))!.QuantidadeEstoque);
        Assert.Equal(9, (await cliente.GetFromJsonAsync<Produto>($"/api/produtos/{b.Id}", Json))!.QuantidadeEstoque);
    }

    [Theory(DisplayName = "CT05 - Quantidade zero ou inválida é recusada")]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(1000)]
    public async Task CT05_QuantidadeInvalida(int quantidade)
    {
        var cliente = await api.NovoClienteCadastradoAsync();
        var r = await cliente.PostAsJsonAsync("/api/pedidos", PedidoEntrega((1, quantidade)));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("quantidade", (await Ler<Erro>(r)).Mensagem);
    }

    [Fact(DisplayName = "CT06 - Finalizar pedido sem login é recusado")]
    public async Task CT06_PedidoSemLogin()
    {
        var r = await api.NovoCliente().PostAsJsonAsync("/api/pedidos", PedidoEntrega((1, 1)));
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Contains("login", (await Ler<Erro>(r)).Mensagem);
    }

    [Fact(DisplayName = "CT07 - Entrega sem endereço não registra o pedido")]
    public async Task CT07_EntregaSemEndereco()
    {
        var cliente = await api.NovoClienteCadastradoAsync();
        var r = await cliente.PostAsJsonAsync("/api/pedidos", new
        {
            tipoEntrega = "ENTREGA", enderecoEntrega = "  ", formaPagamento = "PIX",
            itens = new[] { new { produtoId = 1, quantidade = 1 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("endereço", (await Ler<Erro>(r)).Mensagem);
        Assert.Empty((await cliente.GetFromJsonAsync<List<Pedido>>("/api/pedidos", Json))!);
    }

    [Fact(DisplayName = "CT07b - Retirada não exige endereço")]
    public async Task CT07b_RetiradaSemEndereco()
    {
        var cliente = await api.NovoClienteCadastradoAsync();
        var r = await cliente.PostAsJsonAsync("/api/pedidos", new
        {
            tipoEntrega = "RETIRADA", formaPagamento = "DINHEIRO",
            itens = new[] { new { produtoId = 2, quantidade = 1 } },
        });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var p = await Ler<Pedido>(r);
        Assert.Null(p.EnderecoEntrega);
        Assert.Equal("PRONTO_PARA_RETIRADA", p.Etapas[2].Status);
    }

    [Fact(DisplayName = "CT09 - Quantidade maior que o estoque é recusada sem alterar o estoque")]
    public async Task CT09_EstoqueInsuficiente()
    {
        var admin = await api.AdminAsync();
        var p = await CriarProdutoAsync(admin, "Teste CT09", 10m, 3);
        var cliente = await api.NovoClienteCadastradoAsync();

        var r = await cliente.PostAsJsonAsync("/api/pedidos", PedidoEntrega((p.Id, 4)));
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Contains("apenas 3", (await Ler<Erro>(r)).Mensagem);
        Assert.Equal(3, (await cliente.GetFromJsonAsync<Produto>($"/api/produtos/{p.Id}", Json))!.QuantidadeEstoque);
    }

    [Fact(DisplayName = "CT09b - Pedidos simultâneos não vendem além do estoque")]
    public async Task CT09b_PedidosSimultaneos()
    {
        var admin = await api.AdminAsync();
        var p = await CriarProdutoAsync(admin, "Teste concorrência", 10m, 5);
        var clientes = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => api.NovoClienteCadastradoAsync()));

        // 4 pedidos de 2 unidades ao mesmo tempo, com apenas 5 em estoque: só 2 podem passar.
        var respostas = await Task.WhenAll(clientes.Select(c => c.PostAsJsonAsync("/api/pedidos", PedidoEntrega((p.Id, 2)))));
        Assert.Equal(2, respostas.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.Equal(2, respostas.Count(r => r.StatusCode == HttpStatusCode.Conflict));
        Assert.Equal(1, (await admin.GetFromJsonAsync<Produto>($"/api/admin/produtos/{p.Id}", Json))!.QuantidadeEstoque);
    }

    // ------------------------------------------------------------- Segurança

    [Fact(DisplayName = "CT10 - Cliente não vê pedido de outro cliente")]
    public async Task CT10_PedidoDeOutroCliente()
    {
        var dono = await api.NovoClienteCadastradoAsync();
        var pedido = await Ler<Pedido>(await dono.PostAsJsonAsync("/api/pedidos", PedidoEntrega((1, 1))));

        var outro = await api.NovoClienteCadastradoAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await outro.GetAsync($"/api/pedidos/{pedido.Id}")).StatusCode);
        Assert.DoesNotContain(await outro.GetFromJsonAsync<List<Pedido>>("/api/pedidos", Json) ?? [], p => p.Id == pedido.Id);
        Assert.Equal(HttpStatusCode.OK, (await dono.GetAsync($"/api/pedidos/{pedido.Id}")).StatusCode);
    }

    [Fact(DisplayName = "CT11 - Cliente não acessa funções de administrador")]
    public async Task CT11_ClienteSemAcessoAdmin()
    {
        var cliente = await api.ClienteLogadoAsync("cliente@teste.com", ApiFixture.SenhaCliente);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/admin/pedidos")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsJsonAsync("/api/admin/produtos",
            new { nome = "Invasor", preco = 1, quantidadeEstoque = 1, categoriaId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.NovoCliente().GetAsync("/api/admin/produtos")).StatusCode);
    }

    [Fact(DisplayName = "CT12 - Administrador atualiza o status e o cliente vê a mudança")]
    public async Task CT12_AdminAtualizaStatus()
    {
        var cliente = await api.NovoClienteCadastradoAsync();
        var pedido = await Ler<Pedido>(await cliente.PostAsJsonAsync("/api/pedidos", PedidoEntrega((1, 1))));
        var admin = await api.AdminAsync();

        var invalido = await admin.PatchAsJsonAsync($"/api/admin/pedidos/{pedido.Id}/status", new { status = "ENTREGUE" });
        Assert.Equal(HttpStatusCode.Conflict, invalido.StatusCode);

        foreach (var status in new[] { "EM_PREPARO", "SAIU_PARA_ENTREGA", "ENTREGUE" })
        {
            var r = await admin.PatchAsJsonAsync($"/api/admin/pedidos/{pedido.Id}/status", new { status });
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            Assert.Equal(status, (await cliente.GetFromJsonAsync<Pedido>($"/api/pedidos/{pedido.Id}", Json))!.Status);
        }
        var final = await cliente.GetFromJsonAsync<Pedido>($"/api/pedidos/{pedido.Id}", Json);
        Assert.All(final!.Etapas, e => Assert.True(e.Concluida));
    }

    [Fact(DisplayName = "CT12b - Cancelamento devolve os itens ao estoque")]
    public async Task CT12b_CancelamentoDevolveEstoque()
    {
        var admin = await api.AdminAsync();
        var p = await CriarProdutoAsync(admin, "Teste cancelamento", 10m, 6);
        var cliente = await api.NovoClienteCadastradoAsync();
        var pedido = await Ler<Pedido>(await cliente.PostAsJsonAsync("/api/pedidos", PedidoEntrega((p.Id, 4))));
        Assert.Equal(2, (await admin.GetFromJsonAsync<Produto>($"/api/admin/produtos/{p.Id}", Json))!.QuantidadeEstoque);

        var r = await admin.PatchAsJsonAsync($"/api/admin/pedidos/{pedido.Id}/status", new { status = "CANCELADO" });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(6, (await admin.GetFromJsonAsync<Produto>($"/api/admin/produtos/{p.Id}", Json))!.QuantidadeEstoque);
    }

    [Fact(DisplayName = "CT13 - O servidor ignora preço e total enviados pela interface")]
    public async Task CT13_TotalCalculadoNoServidor()
    {
        var cliente = await api.NovoClienteCadastradoAsync();
        var r = await cliente.PostAsJsonAsync("/api/pedidos", new
        {
            tipoEntrega = "RETIRADA", formaPagamento = "PIX", valorTotal = 0.01m,
            itens = new[] { new { produtoId = 1, quantidade = 2, precoUnitario = 0.01m, subtotal = 0.02m } },
        });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        var pedido = await Ler<Pedido>(r);
        Assert.Equal(24.00m, pedido.ValorTotal);
        Assert.Equal(12.00m, pedido.Itens[0].PrecoUnitario);
    }

    [Fact(DisplayName = "CT14 - Cadastro com e-mail já usado é recusado")]
    public async Task CT14_EmailDuplicado()
    {
        var r = await api.NovoCliente().PostAsJsonAsync("/api/auth/cadastro",
            new { nome = "Outra Pessoa", email = "CLIENTE@teste.com", senha = "segredo123" });
        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Contains("já está cadastrado", (await Ler<Erro>(r)).Mensagem);
    }

    // ------------------------------------------------------------- Autenticação e validação

    [Fact(DisplayName = "RF04 - Login com senha errada é recusado; login certo cria a sessão")]
    public async Task RF04_Login()
    {
        var http = api.NovoCliente();
        var errado = await http.PostAsJsonAsync("/api/auth/login", new { email = "cliente@teste.com", senha = "errada" });
        Assert.Equal(HttpStatusCode.Unauthorized, errado.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/auth/me")).StatusCode);

        var certo = await http.PostAsJsonAsync("/api/auth/login", new { email = " Cliente@Teste.com ", senha = ApiFixture.SenhaCliente });
        Assert.Equal(HttpStatusCode.OK, certo.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/api/auth/me")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await http.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact(DisplayName = "RF09 - Cadastro com dados inválidos devolve mensagens por campo")]
    public async Task RF09_CadastroInvalido()
    {
        var r = await api.NovoCliente().PostAsJsonAsync("/api/auth/cadastro", new { nome = "A", email = "invalido", senha = "123" });
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var corpo = await r.Content.ReadAsStringAsync();
        Assert.Contains("\"nome\"", corpo);
        Assert.Contains("\"email\"", corpo);
        Assert.Contains("\"senha\"", corpo);
    }

    [Fact(DisplayName = "RF07 - Administrador cadastra e altera produto; dados inválidos são recusados")]
    public async Task RF07_GerenciarProdutos()
    {
        var admin = await api.AdminAsync();
        var p = await CriarProdutoAsync(admin, "Café com Avelã", 14.5m, 12, categoriaId: 3);
        Assert.Equal("Especiais", p.Categoria);

        var r = await admin.PutAsJsonAsync($"/api/admin/produtos/{p.Id}",
            new { nome = "Café com Avelã", preco = 15m, quantidadeEstoque = 20, categoriaId = 3, ativo = true });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var alterado = await Ler<Produto>(r);
        Assert.Equal(15m, alterado.Preco);
        Assert.Equal(20, alterado.QuantidadeEstoque);

        var negativo = await admin.PostAsJsonAsync("/api/admin/produtos", new { nome = "X Y", preco = -1, quantidadeEstoque = 1, categoriaId = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, negativo.StatusCode);
        var semCategoria = await admin.PostAsJsonAsync("/api/admin/produtos", new { nome = "X Y", preco = 1, quantidadeEstoque = 1, categoriaId = 999 });
        Assert.Equal(HttpStatusCode.BadRequest, semCategoria.StatusCode);
    }

    [Fact(DisplayName = "Front-end - Páginas principais são servidas")]
    public async Task FrontEnd_PaginasServidas()
    {
        var http = api.NovoCliente();
        foreach (var pagina in new[] { "/", "/produto.html", "/carrinho.html", "/finalizar.html", "/login.html", "/cadastro.html", "/pedidos.html", "/pedido.html", "/perfil.html", "/admin.html", "/js/core.js", "/css/app.css" })
            Assert.Equal(HttpStatusCode.OK, (await http.GetAsync(pagina)).StatusCode);
    }
}
