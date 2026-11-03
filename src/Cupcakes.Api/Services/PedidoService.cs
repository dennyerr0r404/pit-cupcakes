using Cupcakes.Api.Data;
using Cupcakes.Api.Dtos;
using Cupcakes.Api.Models;

namespace Cupcakes.Api.Services;

/// <summary>Regras de negócio dos pedidos: criação, consulta e mudança de status.</summary>
public class PedidoService(PedidoRepositorio repo)
{
    public async Task<Pedido> CriarAsync(int idUsuario, CriarPedidoRequest req, CancellationToken ct = default)
    {
        var pedido = MontarPedido(idUsuario, req);
        var ids = req.Itens.Select(i => i.ProdutoId).Distinct().ToArray();

        await using var con = await repo.Fonte.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);

        // Bloqueia as linhas dos produtos: dois pedidos simultâneos não vendem o mesmo estoque.
        var produtos = await repo.BloquearProdutosAsync(con, tx, ids, ct);

        foreach (var item in req.Itens)
        {
            if (!produtos.TryGetValue(item.ProdutoId, out var produto) || !produto.Ativo)
                throw new RegraNegocioException("Um dos produtos do carrinho não está mais disponível.", 409);
            pedido.AdicionarItem(produto, item.Quantidade); // valida e baixa o estoque em memória
        }

        // O total é calculado aqui, com os preços do banco; nada vem da interface (RNF04).
        pedido.ValorTotal = pedido.CalcularTotal();

        await repo.InserirAsync(con, tx, pedido, ct);
        foreach (var produto in produtos.Values)
            await repo.AtualizarEstoqueAsync(con, tx, produto, ct);
        await tx.CommitAsync(ct);

        return (await repo.BuscarAsync(pedido.IdPedido, ct))!;
    }

    /// <summary>Valida os dados do pedido que não dependem do banco.</summary>
    internal static Pedido MontarPedido(int idUsuario, CriarPedidoRequest req)
    {
        if (!Codigos.TryParseTipoEntrega(req.TipoEntrega, out var tipo))
            throw new RegraNegocioException("Escolha entrega ou retirada.");
        if (!Codigos.TryParseFormaPagamento(req.FormaPagamento, out var pagto))
            throw new RegraNegocioException("Escolha a forma de pagamento: PIX, dinheiro ou cartão.");

        var endereco = string.IsNullOrWhiteSpace(req.EnderecoEntrega) ? null : req.EnderecoEntrega.Trim();
        if (tipo == TipoEntrega.Entrega && (endereco is null || endereco.Length < 5))
            throw new RegraNegocioException("Informe o endereço para pedidos com entrega.");
        if (tipo == TipoEntrega.Retirada)
            endereco = null;

        if (req.Itens is null || req.Itens.Count == 0)
            throw new RegraNegocioException("O pedido não tem itens.");
        if (req.Itens.Count > 50)
            throw new RegraNegocioException("O pedido tem itens demais.");

        return new Pedido
        {
            IdUsuario = idUsuario,
            TipoEntrega = tipo,
            EnderecoEntrega = endereco,
            FormaPagamento = pagto,
            Observacao = string.IsNullOrWhiteSpace(req.Observacao) ? null : req.Observacao.Trim(),
            Status = StatusPedido.Recebido,
        };
    }

    public Task<Pedido?> BuscarAsync(int id, CancellationToken ct = default) => repo.BuscarAsync(id, ct);

    public Task<List<Pedido>> ListarDoClienteAsync(int idUsuario, CancellationToken ct = default) =>
        repo.ListarDoClienteAsync(idUsuario, ct);

    public Task<List<Pedido>> ListarParaAdminAsync(string? situacao, CancellationToken ct = default) =>
        repo.ListarParaAdminAsync(situacao, ct);

    public async Task<Pedido> AtualizarStatusAsync(int id, string novoStatus, CancellationToken ct = default)
    {
        if (!Codigos.TryParseStatus(novoStatus, out var novo))
            throw new RegraNegocioException("Status inválido.");

        await using var con = await repo.Fonte.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);

        var pedido = await repo.BloquearPedidoAsync(con, tx, id, ct)
                     ?? throw new RegraNegocioException("Pedido não encontrado.", 404);
        pedido.AtualizarStatus(novo);
        await repo.AtualizarStatusAsync(con, tx, pedido, ct);

        // Cancelamento devolve os itens ao estoque.
        if (novo == StatusPedido.Cancelado)
        {
            var produtos = await repo.BloquearProdutosAsync(con, tx, pedido.Itens.Select(i => i.IdProduto).ToArray(), ct);
            foreach (var item in pedido.Itens)
            {
                var produto = produtos[item.IdProduto];
                produto.DevolverEstoque(item.Quantidade);
                await repo.AtualizarEstoqueAsync(con, tx, produto, ct);
            }
        }

        await tx.CommitAsync(ct);
        return pedido;
    }
}
