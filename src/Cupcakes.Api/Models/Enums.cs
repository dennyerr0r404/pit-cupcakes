namespace Cupcakes.Api.Models;

public enum PerfilUsuario { Cliente, Admin }

public enum StatusPedido { Recebido, EmPreparo, ProntoParaRetirada, SaiuParaEntrega, Entregue, Cancelado }

public enum TipoEntrega { Entrega, Retirada }

public enum FormaPagamento { Dinheiro, Pix, Cartao }

/// <summary>
/// Conversão entre os enums do C# e os códigos gravados no banco/enviados pela API
/// (ex.: StatusPedido.EmPreparo &lt;-&gt; "EM_PREPARO").
/// </summary>
public static class Codigos
{
    private static readonly Dictionary<PerfilUsuario, string> Perfis = new()
    {
        [PerfilUsuario.Cliente] = "CLIENTE",
        [PerfilUsuario.Admin] = "ADMIN",
    };

    private static readonly Dictionary<StatusPedido, string> Status = new()
    {
        [StatusPedido.Recebido] = "RECEBIDO",
        [StatusPedido.EmPreparo] = "EM_PREPARO",
        [StatusPedido.ProntoParaRetirada] = "PRONTO_PARA_RETIRADA",
        [StatusPedido.SaiuParaEntrega] = "SAIU_PARA_ENTREGA",
        [StatusPedido.Entregue] = "ENTREGUE",
        [StatusPedido.Cancelado] = "CANCELADO",
    };

    private static readonly Dictionary<TipoEntrega, string> Tipos = new()
    {
        [TipoEntrega.Entrega] = "ENTREGA",
        [TipoEntrega.Retirada] = "RETIRADA",
    };

    private static readonly Dictionary<FormaPagamento, string> Pagamentos = new()
    {
        [FormaPagamento.Dinheiro] = "DINHEIRO",
        [FormaPagamento.Pix] = "PIX",
        [FormaPagamento.Cartao] = "CARTAO",
    };

    public static string Codigo(this PerfilUsuario v) => Perfis[v];
    public static string Codigo(this StatusPedido v) => Status[v];
    public static string Codigo(this TipoEntrega v) => Tipos[v];
    public static string Codigo(this FormaPagamento v) => Pagamentos[v];

    public static PerfilUsuario ParsePerfil(string s) => Parse(Perfis, s, "perfil");
    public static StatusPedido ParseStatus(string s) => Parse(Status, s, "status");
    public static TipoEntrega ParseTipoEntrega(string s) => Parse(Tipos, s, "tipo de entrega");
    public static FormaPagamento ParseFormaPagamento(string s) => Parse(Pagamentos, s, "forma de pagamento");

    public static bool TryParseStatus(string? s, out StatusPedido v) => TryParse(Status, s, out v);
    public static bool TryParseTipoEntrega(string? s, out TipoEntrega v) => TryParse(Tipos, s, out v);
    public static bool TryParseFormaPagamento(string? s, out FormaPagamento v) => TryParse(Pagamentos, s, out v);

    private static T Parse<T>(Dictionary<T, string> mapa, string s, string nome) where T : struct =>
        TryParse(mapa, s, out var v) ? v : throw new ArgumentException($"Valor inválido para {nome}: {s}");

    private static bool TryParse<T>(Dictionary<T, string> mapa, string? s, out T valor) where T : struct
    {
        foreach (var (k, codigo) in mapa)
        {
            if (string.Equals(codigo, s?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                valor = k;
                return true;
            }
        }
        valor = default;
        return false;
    }
}
