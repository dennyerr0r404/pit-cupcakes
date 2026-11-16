using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Cupcakes.Api.Models;
using Xunit;

namespace Cupcakes.Tests;

/// <summary>Regras do feedback que não dependem do banco (RF12).</summary>
public class FeedbackDominioTests
{
    private static Feedback Novo(AssuntoFeedback assunto = AssuntoFeedback.Atendimento, int nota = 4,
        string comentario = "Muito bom", int? produto = null) =>
        new() { Assunto = assunto, Nota = nota, Comentario = comentario, IdProduto = produto };

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void NotaForaDe1a5_Recusa(int nota) =>
        Assert.Throws<RegraNegocioException>(() => Novo(nota: nota).Validar());

    [Fact]
    public void ComentarioCurtoOuSoEspacos_Recusa() =>
        Assert.Throws<RegraNegocioException>(() => Novo(comentario: "  a  ").Validar());

    [Fact]
    public void ComentarioMuitoLongo_Recusa() =>
        Assert.Throws<RegraNegocioException>(() => Novo(comentario: new string('x', 1001)).Validar());

    [Fact]
    public void ProdutoSemIdentificacao_Recusa() =>
        Assert.Throws<RegraNegocioException>(() => Novo(AssuntoFeedback.Produto).Validar());

    [Fact]
    public void Atendimento_DescartaProdutoEApagaEspacos()
    {
        var f = Novo(AssuntoFeedback.Atendimento, comentario: "  Entrega rápida  ", produto: 3);
        f.Validar();
        Assert.Null(f.IdProduto);
        Assert.Equal("Entrega rápida", f.Comentario);
    }
}

/// <summary>Feedback pela API, com aplicação e PostgreSQL reais (CT15 a CT17).</summary>
public class FeedbackApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private record Feedback(int Id, string Assunto, int Nota, string Comentario, bool Lido,
        int? PedidoId, int? ProdutoId, string? Produto, string? Cliente);
    private record Resumo(int Total, int NaoLidos, decimal? MediaProduto, decimal? MediaAtendimento);
    private record Painel(Resumo Resumo, List<Feedback> Itens);
    private record Pedido(int Id);
    private record Erro(string Mensagem);

    private static async Task<int> FazerPedidoAsync(HttpClient cliente, int produtoId)
    {
        var r = await cliente.PostAsJsonAsync("/api/pedidos", new
        {
            tipoEntrega = "RETIRADA", formaPagamento = "PIX",
            itens = new[] { new { produtoId, quantidade = 1 } },
        });
        Assert.Equal(HttpStatusCode.Created, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<Pedido>(Json))!.Id;
    }

    [Fact(DisplayName = "CT15 - Cliente envia feedback de produto do pedido e de atendimento")]
    public async Task CT15_EnviarFeedback()
    {
        var cliente = await api.NovoClienteCadastradoAsync();
        var pedido = await FazerPedidoAsync(cliente, 1);

        var r1 = await cliente.PostAsJsonAsync("/api/feedbacks",
            new { assunto = "PRODUTO", produtoId = 1, pedidoId = pedido, nota = 5, comentario = "  Delicioso!  " });
        Assert.Equal(HttpStatusCode.Created, r1.StatusCode);
        var fb = (await r1.Content.ReadFromJsonAsync<Feedback>(Json))!;
        Assert.Equal("Velvet Red", fb.Produto);
        Assert.Equal("Delicioso!", fb.Comentario);
        Assert.False(fb.Lido);
        Assert.Null(fb.Cliente); // dados do cliente só aparecem para o administrador

        var r2 = await cliente.PostAsJsonAsync("/api/feedbacks",
            new { assunto = "ATENDIMENTO", nota = 3, comentario = "Demorou um pouco", produtoId = 2 });
        Assert.Equal(HttpStatusCode.Created, r2.StatusCode);
        Assert.Null((await r2.Content.ReadFromJsonAsync<Feedback>(Json))!.ProdutoId);

        var meus = await cliente.GetFromJsonAsync<List<Feedback>>("/api/feedbacks", Json);
        Assert.Equal(2, meus!.Count);
    }

    [Theory(DisplayName = "CT16 - Feedback inválido é recusado")]
    [InlineData("PRODUTO", null, 5, "Bom demais")]      // produto não informado
    [InlineData("ATENDIMENTO", null, 0, "Bom demais")]  // nota fora de 1 a 5
    [InlineData("ATENDIMENTO", null, 6, "Bom demais")]
    [InlineData("ATENDIMENTO", null, 4, "ok")]          // comentário curto
    [InlineData("PRECO", null, 4, "Bom demais")]        // assunto inválido
    [InlineData("PRODUTO", 99999, 4, "Bom demais")]     // produto inexistente
    public async Task CT16_FeedbackInvalido(string assunto, int? produtoId, int nota, string comentario)
    {
        var cliente = await api.NovoClienteCadastradoAsync();
        var r = await cliente.PostAsJsonAsync("/api/feedbacks", new { assunto, produtoId, nota, comentario });
        Assert.True(r.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound, r.StatusCode.ToString());
        Assert.Empty((await cliente.GetFromJsonAsync<List<Feedback>>("/api/feedbacks", Json))!);
    }

    [Fact(DisplayName = "CT16b - Sem login, com pedido de outro cliente ou com produto fora do pedido é recusado")]
    public async Task CT16b_FeedbackSemPermissao()
    {
        var corpo = new { assunto = "ATENDIMENTO", nota = 4, comentario = "Gostei" };
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.NovoCliente().PostAsJsonAsync("/api/feedbacks", corpo)).StatusCode);

        var dono = await api.NovoClienteCadastradoAsync();
        var pedido = await FazerPedidoAsync(dono, 2);

        var outro = await api.NovoClienteCadastradoAsync();
        var r1 = await outro.PostAsJsonAsync("/api/feedbacks", new { assunto = "ATENDIMENTO", nota = 4, comentario = "Gostei", pedidoId = pedido });
        Assert.Equal(HttpStatusCode.NotFound, r1.StatusCode);

        var r2 = await dono.PostAsJsonAsync("/api/feedbacks", new { assunto = "PRODUTO", produtoId = 1, pedidoId = pedido, nota = 4, comentario = "Gostei" });
        Assert.Equal(HttpStatusCode.BadRequest, r2.StatusCode);
        Assert.Contains("não faz parte do pedido", (await r2.Content.ReadFromJsonAsync<Erro>(Json))!.Mensagem);
    }

    [Fact(DisplayName = "CT17 - Administrador vê os feedbacks com as médias e marca como lido")]
    public async Task CT17_AdminFeedbacks()
    {
        var cliente = await api.NovoClienteCadastradoAsync();
        var r = await cliente.PostAsJsonAsync("/api/feedbacks", new { assunto = "ATENDIMENTO", nota = 2, comentario = "Atendimento lento" });
        var enviado = (await r.Content.ReadFromJsonAsync<Feedback>(Json))!;

        var admin = await api.AdminAsync();
        var painel = await admin.GetFromJsonAsync<Painel>("/api/admin/feedbacks?situacao=nao_lidos", Json);
        Assert.Contains(painel!.Itens, f => f.Id == enviado.Id && f.Cliente == "Cliente Teste");
        Assert.True(painel.Resumo.NaoLidos >= 1);
        Assert.True(painel.Resumo.MediaAtendimento.HasValue);
        Assert.InRange(painel.Resumo.MediaAtendimento!.Value, 1m, 5m);

        var lido = await admin.PatchAsJsonAsync($"/api/admin/feedbacks/{enviado.Id}/lido", new { lido = true });
        Assert.Equal(HttpStatusCode.OK, lido.StatusCode);
        var depois = await admin.GetFromJsonAsync<Painel>("/api/admin/feedbacks?situacao=nao_lidos", Json);
        Assert.DoesNotContain(depois!.Itens, f => f.Id == enviado.Id);
        Assert.Equal(painel.Resumo.NaoLidos - 1, depois.Resumo.NaoLidos);

        Assert.Equal(HttpStatusCode.NotFound,
            (await admin.PatchAsJsonAsync("/api/admin/feedbacks/99999/lido", new { lido = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/admin/feedbacks")).StatusCode);
    }
}
