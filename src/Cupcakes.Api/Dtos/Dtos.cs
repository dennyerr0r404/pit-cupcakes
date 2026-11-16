using System.ComponentModel.DataAnnotations;

namespace Cupcakes.Api.Dtos;

// =====================================================================
// Objetos de entrada (requisições) — validados automaticamente pelo MVC.
// Objetos de saída (respostas) — records imutáveis.
// =====================================================================

// ---------------- Autenticação ----------------
public class CadastroRequest
{
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 120 caracteres.")]
    public string Nome { get; set; } = "";

    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(180, ErrorMessage = "O e-mail deve ter no máximo 180 caracteres.")]
    public string Email { get; set; } = "";

    [StringLength(20, ErrorMessage = "O telefone deve ter no máximo 20 caracteres.")]
    public string? Telefone { get; set; }

    [Required(ErrorMessage = "Informe a senha.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "A senha deve ter pelo menos 6 caracteres.")]
    public string Senha { get; set; } = "";
}

public class LoginRequest
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Informe a senha.")]
    public string Senha { get; set; } = "";
}

public class PerfilRequest
{
    [Required(ErrorMessage = "Informe o nome.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 120 caracteres.")]
    public string Nome { get; set; } = "";

    [StringLength(20, ErrorMessage = "O telefone deve ter no máximo 20 caracteres.")]
    public string? Telefone { get; set; }
}

public class TrocarSenhaRequest
{
    [Required(ErrorMessage = "Informe a senha atual.")]
    public string SenhaAtual { get; set; } = "";

    [Required(ErrorMessage = "Informe a nova senha.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "A nova senha deve ter pelo menos 6 caracteres.")]
    public string NovaSenha { get; set; } = "";
}

public class EsqueciSenhaRequest
{
    [Required(ErrorMessage = "Informe o e-mail.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    public string Email { get; set; } = "";
}

public class RedefinirSenhaRequest
{
    [Required(ErrorMessage = "Link de redefinição inválido.")]
    public string Codigo { get; set; } = "";

    [Required(ErrorMessage = "Informe a nova senha.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "A nova senha deve ter pelo menos 6 caracteres.")]
    public string NovaSenha { get; set; } = "";
}

// ---------------- Produtos (administração) ----------------
public class ProdutoSalvarRequest
{
    [Required(ErrorMessage = "Informe o nome do produto.")]
    [StringLength(120, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 120 caracteres.")]
    public string Nome { get; set; } = "";

    public string? Descricao { get; set; }

    [Range(typeof(decimal), "0", "99999999", ErrorMessage = "O preço não pode ser negativo.")]
    public decimal Preco { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "O estoque não pode ser negativo.")]
    public int QuantidadeEstoque { get; set; }

    [StringLength(255, ErrorMessage = "A URL da imagem deve ter no máximo 255 caracteres.")]
    public string? ImagemUrl { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Selecione a categoria.")]
    public int CategoriaId { get; set; }

    public bool Ativo { get; set; } = true;
}

public class AtivoRequest
{
    public bool Ativo { get; set; }
}

// ---------------- Pedidos ----------------
public class ItemPedidoRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "Produto inválido.")]
    public int ProdutoId { get; set; }

    [Range(1, 999, ErrorMessage = "A quantidade deve ser de 1 a 999.")]
    public int Quantidade { get; set; }
}

/// <summary>
/// Dados enviados pelo cliente ao finalizar. Não há campos de preço ou total:
/// o servidor calcula tudo a partir do banco (RNF04).
/// </summary>
public class CriarPedidoRequest
{
    [Required(ErrorMessage = "Escolha entrega ou retirada.")]
    public string TipoEntrega { get; set; } = "";

    [StringLength(255, ErrorMessage = "O endereço deve ter no máximo 255 caracteres.")]
    public string? EnderecoEntrega { get; set; }

    [Required(ErrorMessage = "Escolha a forma de pagamento.")]
    public string FormaPagamento { get; set; } = "";

    [StringLength(255, ErrorMessage = "A observação deve ter no máximo 255 caracteres.")]
    public string? Observacao { get; set; }

    [Required(ErrorMessage = "O pedido não tem itens.")]
    [MinLength(1, ErrorMessage = "O pedido não tem itens.")]
    public List<ItemPedidoRequest> Itens { get; set; } = [];
}

public class AtualizarStatusRequest
{
    [Required(ErrorMessage = "Informe o novo status.")]
    public string Status { get; set; } = "";
}

// ---------------- Feedback ----------------
public class CriarFeedbackRequest
{
    [Required(ErrorMessage = "Escolha se o feedback é sobre um produto ou sobre o atendimento.")]
    public string Assunto { get; set; } = "";

    [Range(1, 5, ErrorMessage = "Escolha uma nota de 1 a 5 estrelas.")]
    public int Nota { get; set; }

    [Required(ErrorMessage = "Escreva o seu comentário.")]
    [StringLength(1000, MinimumLength = 3, ErrorMessage = "O comentário deve ter entre 3 e 1000 caracteres.")]
    public string Comentario { get; set; } = "";

    /// <summary>Pedido a que o feedback se refere (opcional).</summary>
    public int? PedidoId { get; set; }

    /// <summary>Produto avaliado (obrigatório quando o assunto é PRODUTO).</summary>
    public int? ProdutoId { get; set; }
}

public class LidoRequest
{
    public bool Lido { get; set; }
}

// ---------------- Respostas ----------------
public record UsuarioDto(int Id, string Nome, string Email, string? Telefone, string Perfil);

public record LoginResponse(UsuarioDto Usuario);

public record EsqueciSenhaResponse(string Mensagem, string? Link);

public record CategoriaDto(int Id, string Nome, string? Descricao);

public record ProdutoDto(
    int Id, string Nome, string? Descricao, decimal Preco, int QuantidadeEstoque,
    bool Disponivel, string? ImagemUrl, int CategoriaId, string Categoria, bool Ativo);

public record ItemPedidoDto(int ProdutoId, string Produto, string? ImagemUrl, int Quantidade, decimal PrecoUnitario, decimal Subtotal);

public record EtapaDto(string Status, string Rotulo, bool Concluida, bool Atual);

public record PedidoDto(
    int Id, DateTime DataPedido, DateTime DataAtualizacao, string Status, string StatusRotulo,
    decimal ValorTotal, string TipoEntrega, string? EnderecoEntrega, string FormaPagamento,
    string? Observacao, List<ItemPedidoDto> Itens, List<EtapaDto> Etapas,
    string? Cliente, string? ClienteEmail, string? ClienteTelefone, List<string> ProximosStatus);

public record FeedbackDto(
    int Id, string Assunto, int Nota, string Comentario, DateTime DataEnvio, bool Lido,
    int? PedidoId, int? ProdutoId, string? Produto, string? Cliente, string? ClienteEmail);

public record ResumoFeedbackDto(int Total, int NaoLidos, decimal? MediaProduto, decimal? MediaAtendimento);

public record PainelFeedbackDto(ResumoFeedbackDto Resumo, List<FeedbackDto> Itens);
