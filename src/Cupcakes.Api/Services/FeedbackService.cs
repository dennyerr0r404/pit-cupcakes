using Cupcakes.Api.Data;
using Cupcakes.Api.Dtos;
using Cupcakes.Api.Models;

namespace Cupcakes.Api.Services;

/// <summary>Regras do feedback do cliente (RF12).</summary>
public class FeedbackService(FeedbackRepositorio feedbacks, PedidoRepositorio pedidos, ProdutoRepositorio produtos)
{
    public async Task<Feedback> EnviarAsync(int idUsuario, CriarFeedbackRequest req, CancellationToken ct = default)
    {
        if (!Codigos.TryParseAssunto(req.Assunto, out var assunto))
            throw new RegraNegocioException("Escolha se o feedback é sobre um produto ou sobre o atendimento.");

        var feedback = new Feedback
        {
            IdUsuario = idUsuario,
            Assunto = assunto,
            Nota = req.Nota,
            Comentario = req.Comentario,
            IdPedido = req.PedidoId,
            IdProduto = req.ProdutoId,
        };
        feedback.Validar();

        // O pedido informado precisa ser do próprio cliente.
        Pedido? pedido = null;
        if (feedback.IdPedido is int idPedido)
        {
            pedido = await pedidos.BuscarAsync(idPedido, ct);
            if (pedido is null || pedido.IdUsuario != idUsuario)
                throw new RegraNegocioException("Pedido não encontrado.", 404);
        }

        if (feedback.IdProduto is int idProduto)
        {
            if (await produtos.BuscarAsync(idProduto, ct) is null)
                throw new RegraNegocioException("Produto não encontrado.", 404);
            // Avaliando um produto a partir de um pedido: ele precisa fazer parte do pedido.
            if (pedido is not null && pedido.Itens.All(i => i.IdProduto != idProduto))
                throw new RegraNegocioException("Este produto não faz parte do pedido informado.");
        }

        await feedbacks.InserirAsync(feedback, ct);
        return (await feedbacks.BuscarAsync(feedback.IdFeedback, ct))!;
    }
}
