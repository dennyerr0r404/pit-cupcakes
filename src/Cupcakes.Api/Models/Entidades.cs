namespace Cupcakes.Api.Models;

/// <summary>Exceção de regra de negócio: vira resposta HTTP com a mensagem para o usuário.</summary>
public class RegraNegocioException(string mensagem, int statusCode = 400) : Exception(mensagem)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>Erro de regra ligado a um campo do formulário (a tela destaca o campo).</summary>
public class CampoInvalidoException(string campo, string mensagem, int statusCode = 400)
    : RegraNegocioException(mensagem, statusCode)
{
    public string Campo { get; } = campo;
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

/// <summary>Opinião do cliente sobre um produto ou sobre o atendimento (RF12).</summary>
public class Feedback
{
    public const int TamanhoMinimoComentario = 3;
    public const int TamanhoMaximoComentario = 1000;

    public int IdFeedback { get; set; }
    public int IdUsuario { get; set; }
    public int? IdPedido { get; set; }
    public int? IdProduto { get; set; }
    public AssuntoFeedback Assunto { get; set; }
    public int Nota { get; set; }
    public string Comentario { get; set; } = "";
    public DateTime DataEnvio { get; set; }
    public bool Lido { get; set; }

    // Dados de exibição (preenchidos nas consultas)
    public string? NomeProduto { get; set; }
    public string? NomeCliente { get; set; }
    public string? EmailCliente { get; set; }

    /// <summary>Valida as regras que não dependem do banco.</summary>
    public void Validar()
    {
        if (Nota is < 1 or > 5)
            throw new RegraNegocioException("Escolha uma nota de 1 a 5 estrelas.");
        Comentario = (Comentario ?? "").Trim();
        if (Comentario.Length < TamanhoMinimoComentario)
            throw new RegraNegocioException("Escreva um comentário com pelo menos 3 caracteres.");
        if (Comentario.Length > TamanhoMaximoComentario)
            throw new RegraNegocioException("O comentário deve ter no máximo 1000 caracteres.");
        if (Assunto == AssuntoFeedback.Produto && IdProduto is null)
            throw new RegraNegocioException("Escolha o produto que você quer avaliar.");
        if (Assunto == AssuntoFeedback.Atendimento)
            IdProduto = null;
    }
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
