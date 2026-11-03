namespace Cupcakes.Api.Models;

/// <summary>Exceção de regra de negócio: vira resposta HTTP com a mensagem para o usuário.</summary>
public class RegraNegocioException(string mensagem, int statusCode = 400) : Exception(mensagem)
{
    public int StatusCode { get; } = statusCode;
}

public class Usuario
{
    public int IdUsuario { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telefone { get; set; }
    public string SenhaHash { get; set; } = "";
    public PerfilUsuario Perfil { get; set; } = PerfilUsuario.Cliente;
    public DateTime DataCadastro { get; set; }

    public bool EhAdministrador() => Perfil == PerfilUsuario.Admin;
}

public class Categoria
{
    public int IdCategoria { get; set; }
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public int OrdemExibicao { get; set; }
    public bool Ativo { get; set; } = true;
}

public class Produto
{
    public int IdProduto { get; set; }
    public int IdCategoria { get; set; }
    public string NomeCategoria { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Descricao { get; set; }
    public decimal Preco { get; set; }
    public int QuantidadeEstoque { get; set; }
    public string? ImagemUrl { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataCadastro { get; set; }

    public bool EstaDisponivel(int quantidade) => Ativo && quantidade > 0 && QuantidadeEstoque >= quantidade;

    public void BaixarEstoque(int quantidade)
    {
        if (!EstaDisponivel(quantidade))
            throw new RegraNegocioException(
                QuantidadeEstoque <= 0 || !Ativo
                    ? $"O produto \"{Nome}\" não está disponível no momento."
                    : $"Há apenas {QuantidadeEstoque} unidade(s) de \"{Nome}\" em estoque.", 409);
        QuantidadeEstoque -= quantidade;
    }

    public void DevolverEstoque(int quantidade) => QuantidadeEstoque += quantidade;
}

public class Pedido
{
    public int IdPedido { get; set; }
    public int IdUsuario { get; set; }
    public DateTime DataPedido { get; set; }
    public DateTime DataAtualizacao { get; set; }
    public StatusPedido Status { get; set; } = StatusPedido.Recebido;
    public decimal ValorTotal { get; set; }
    public TipoEntrega TipoEntrega { get; set; }
    public string? EnderecoEntrega { get; set; }
    public FormaPagamento FormaPagamento { get; set; }
    public string? Observacao { get; set; }

    // Dados do cliente (preenchidos nas consultas da administração)
    public string? NomeCliente { get; set; }
    public string? EmailCliente { get; set; }
    public string? TelefoneCliente { get; set; }

    public List<ItemPedido> Itens { get; set; } = [];

    /// <summary>Adiciona o produto copiando o preço atual (preserva o histórico) e baixa o estoque.</summary>
    public void AdicionarItem(Produto produto, int quantidade)
    {
        if (quantidade <= 0)
            throw new RegraNegocioException("A quantidade de cada item deve ser maior que zero.");
        if (Itens.Any(i => i.IdProduto == produto.IdProduto))
            throw new RegraNegocioException($"O produto \"{produto.Nome}\" aparece mais de uma vez no pedido.");
        produto.BaixarEstoque(quantidade);
        Itens.Add(new ItemPedido
        {
            IdProduto = produto.IdProduto,
            NomeProduto = produto.Nome,
            ImagemUrl = produto.ImagemUrl,
            Quantidade = quantidade,
            PrecoUnitario = produto.Preco,
        });
    }

    /// <summary>Total calculado no servidor a partir dos itens (RNF04).</summary>
    public decimal CalcularTotal() => Itens.Sum(i => i.CalcularSubtotal());

    public void AtualizarStatus(StatusPedido novo)
    {
        if (!FluxoStatus.PodeMudar(this, novo))
            throw new RegraNegocioException(
                $"Não é possível mudar o pedido de {Status.Codigo()} para {novo.Codigo()}.", 409);
        Status = novo;
    }
}

public class ItemPedido
{
    public int IdItem { get; set; }
    public int IdPedido { get; set; }
    public int IdProduto { get; set; }
    public string NomeProduto { get; set; } = "";
    public string? ImagemUrl { get; set; }
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }

    /// <summary>Subtotal: no banco é a coluna calculada quantidade × preco_unitario.</summary>
    public decimal CalcularSubtotal() => Quantidade * PrecoUnitario;
}

/// <summary>Transições de status permitidas (seção 12.1 da documentação).</summary>
public static class FluxoStatus
{
    public static IReadOnlyList<StatusPedido> Proximos(Pedido p) => p.Status switch
    {
        StatusPedido.Recebido => [StatusPedido.EmPreparo, StatusPedido.Cancelado],
        StatusPedido.EmPreparo => p.TipoEntrega == TipoEntrega.Entrega
            ? [StatusPedido.SaiuParaEntrega, StatusPedido.Cancelado]
            : [StatusPedido.ProntoParaRetirada, StatusPedido.Cancelado],
        StatusPedido.ProntoParaRetirada or StatusPedido.SaiuParaEntrega => [StatusPedido.Entregue],
        _ => [],
    };

    public static bool PodeMudar(Pedido p, StatusPedido novo) => Proximos(p).Contains(novo);

    public static bool EstaAberto(StatusPedido s) => s is not (StatusPedido.Entregue or StatusPedido.Cancelado);

    /// <summary>Etapas exibidas na linha do tempo do cliente.</summary>
    public static IReadOnlyList<StatusPedido> Etapas(TipoEntrega tipo) => tipo == TipoEntrega.Entrega
        ? [StatusPedido.Recebido, StatusPedido.EmPreparo, StatusPedido.SaiuParaEntrega, StatusPedido.Entregue]
        : [StatusPedido.Recebido, StatusPedido.EmPreparo, StatusPedido.ProntoParaRetirada, StatusPedido.Entregue];
}
