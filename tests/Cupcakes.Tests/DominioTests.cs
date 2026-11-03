using Cupcakes.Api.Dtos;
using Cupcakes.Api.Models;
using Cupcakes.Api.Services;
using Xunit;

namespace Cupcakes.Tests;

/// <summary>Testes unitários das regras de negócio (não precisam de banco).</summary>
public class DominioTests
{
    private static Produto NovoProduto(int id = 1, decimal preco = 12m, int estoque = 10, bool ativo = true) =>
        new() { IdProduto = id, Nome = $"Produto {id}", Preco = preco, QuantidadeEstoque = estoque, Ativo = ativo };

    [Fact]
    public void CalcularTotal_SomaQuantidadeVezesPrecoDeCadaItem()
    {
        var pedido = new Pedido();
        pedido.AdicionarItem(NovoProduto(1, 12m), 2);
        pedido.AdicionarItem(NovoProduto(2, 12m), 1);

        Assert.Equal(36m, pedido.CalcularTotal());
        Assert.Equal(24m, pedido.Itens[0].CalcularSubtotal());
    }

    [Fact]
    public void AdicionarItem_CopiaPrecoDoMomentoDaCompra()
    {
        var produto = NovoProduto(preco: 15m);
        var pedido = new Pedido();
        pedido.AdicionarItem(produto, 1);
        produto.Preco = 20m; // preço muda depois da compra

        Assert.Equal(15m, pedido.Itens[0].PrecoUnitario);
        Assert.Equal(15m, pedido.CalcularTotal());
    }

    [Fact]
    public void AdicionarItem_BaixaEstoque()
    {
        var produto = NovoProduto(estoque: 5);
        new Pedido().AdicionarItem(produto, 3);
        Assert.Equal(2, produto.QuantidadeEstoque);
    }

    [Fact]
    public void AdicionarItem_AcimaDoEstoque_Recusa()
    {
        var produto = NovoProduto(estoque: 2);
        var ex = Assert.Throws<RegraNegocioException>(() => new Pedido().AdicionarItem(produto, 3));
        Assert.Contains("apenas 2", ex.Message);
        Assert.Equal(409, ex.StatusCode);
        Assert.Equal(2, produto.QuantidadeEstoque);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AdicionarItem_QuantidadeInvalida_Recusa(int quantidade)
    {
        Assert.Throws<RegraNegocioException>(() => new Pedido().AdicionarItem(NovoProduto(), quantidade));
    }

    [Fact]
    public void AdicionarItem_ProdutoInativo_Recusa()
    {
        Assert.Throws<RegraNegocioException>(() => new Pedido().AdicionarItem(NovoProduto(ativo: false), 1));
    }

    [Fact]
    public void AdicionarItem_ProdutoRepetido_Recusa()
    {
        var pedido = new Pedido();
        var produto = NovoProduto();
        pedido.AdicionarItem(produto, 1);
        Assert.Throws<RegraNegocioException>(() => pedido.AdicionarItem(produto, 1));
    }

    [Theory]
    [InlineData("ENTREGA", "RECEBIDO", "EM_PREPARO", true)]
    [InlineData("ENTREGA", "RECEBIDO", "CANCELADO", true)]
    [InlineData("ENTREGA", "RECEBIDO", "ENTREGUE", false)]
    [InlineData("ENTREGA", "EM_PREPARO", "SAIU_PARA_ENTREGA", true)]
    [InlineData("ENTREGA", "EM_PREPARO", "PRONTO_PARA_RETIRADA", false)]
    [InlineData("RETIRADA", "EM_PREPARO", "PRONTO_PARA_RETIRADA", true)]
    [InlineData("RETIRADA", "EM_PREPARO", "SAIU_PARA_ENTREGA", false)]
    [InlineData("ENTREGA", "SAIU_PARA_ENTREGA", "ENTREGUE", true)]
    [InlineData("RETIRADA", "PRONTO_PARA_RETIRADA", "ENTREGUE", true)]
    [InlineData("ENTREGA", "ENTREGUE", "CANCELADO", false)]
    [InlineData("ENTREGA", "CANCELADO", "EM_PREPARO", false)]
    public void FluxoStatus_RespeitaTransicoesPermitidas(string tipo, string atual, string novo, bool permitido)
    {
        var pedido = new Pedido { TipoEntrega = Codigos.ParseTipoEntrega(tipo), Status = Codigos.ParseStatus(atual) };
        Assert.Equal(permitido, FluxoStatus.PodeMudar(pedido, Codigos.ParseStatus(novo)));
        if (!permitido)
            Assert.Throws<RegraNegocioException>(() => pedido.AtualizarStatus(Codigos.ParseStatus(novo)));
    }

    [Fact]
    public void Codigos_ConvertemNosDoisSentidos()
    {
        foreach (var s in Enum.GetValues<StatusPedido>())
            Assert.Equal(s, Codigos.ParseStatus(s.Codigo()));
        Assert.Equal("PRONTO_PARA_RETIRADA", StatusPedido.ProntoParaRetirada.Codigo());
        Assert.Equal(FormaPagamento.Cartao, Codigos.ParseFormaPagamento("cartao"));
        Assert.False(Codigos.TryParseFormaPagamento("BOLETO", out _));
    }

    [Fact]
    public void MontarPedido_EntregaSemEndereco_Recusa()
    {
        var req = new CriarPedidoRequest { TipoEntrega = "ENTREGA", FormaPagamento = "PIX", Itens = [new() { ProdutoId = 1, Quantidade = 1 }] };
        var ex = Assert.Throws<RegraNegocioException>(() => PedidoService.MontarPedido(1, req));
        Assert.Contains("endereço", ex.Message);
    }

    [Fact]
    public void MontarPedido_RetiradaIgnoraEndereco()
    {
        var req = new CriarPedidoRequest
        {
            TipoEntrega = "RETIRADA", EnderecoEntrega = "Rua X, 1", FormaPagamento = "DINHEIRO",
            Itens = [new() { ProdutoId = 1, Quantidade = 1 }],
        };
        var pedido = PedidoService.MontarPedido(1, req);
        Assert.Equal(TipoEntrega.Retirada, pedido.TipoEntrega);
        Assert.Null(pedido.EnderecoEntrega);
        Assert.Equal(StatusPedido.Recebido, pedido.Status);
    }

    [Fact]
    public void MontarPedido_FormaDePagamentoInvalida_Recusa()
    {
        var req = new CriarPedidoRequest { TipoEntrega = "RETIRADA", FormaPagamento = "BOLETO", Itens = [new() { ProdutoId = 1, Quantidade = 1 }] };
        Assert.Throws<RegraNegocioException>(() => PedidoService.MontarPedido(1, req));
    }

    [Fact]
    public void Senha_HashNaoGuardaTextoEConfere()
    {
        var servico = new SenhaService();
        var hash = servico.GerarHash("Segredo@1");

        Assert.DoesNotContain("Segredo@1", hash);
        Assert.NotEqual(hash, servico.GerarHash("Segredo@1")); // sal aleatório
        Assert.True(servico.Verificar(hash, "Segredo@1").Confere);
        Assert.False(servico.Verificar(hash, "segredo@1").Confere);
        Assert.False(servico.Verificar("hash-invalido", "x").Confere);
    }

    [Fact]
    public void Mapeamento_EtapasDeRetiradaECancelamento()
    {
        var retirada = new Pedido { TipoEntrega = TipoEntrega.Retirada, Status = StatusPedido.EmPreparo };
        var dto = retirada.ToDto();
        Assert.Equal(["RECEBIDO", "EM_PREPARO", "PRONTO_PARA_RETIRADA", "ENTREGUE"], dto.Etapas.Select(e => e.Status).ToList());
        Assert.True(dto.Etapas[1].Atual);

        var cancelado = new Pedido { TipoEntrega = TipoEntrega.Entrega, Status = StatusPedido.Cancelado }.ToDto();
        Assert.Equal("Cancelado", cancelado.StatusRotulo);
        Assert.Equal(2, cancelado.Etapas.Count);
    }
}
