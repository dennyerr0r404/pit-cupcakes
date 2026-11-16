using System.Text.RegularExpressions;

namespace Cupcakes.Api.Models;

/// <summary>
/// Validação e formatação de telefone brasileiro (Laudo, item 2).
/// Aceita celular com 9 dígitos ou fixo com 8, sempre com DDD:
/// "(11) 98888-7777" ou "(11) 3333-4444". Também aceita a digitação sem
/// máscara, com +55 ou com 0 antes do DDD, e devolve no formato padrão.
/// </summary>
public static partial class Telefone
{
    public const string MensagemInvalido = "Informe o telefone com DDD, ex.: (11) 98888-7777.";

    [GeneratedRegex(@"^[1-9][0-9]9[0-9]{8}$")] private static partial Regex Celular();
    [GeneratedRegex(@"^[1-9][0-9][2-8][0-9]{7}$")] private static partial Regex Fixo();

    /// <summary>
    /// Vazio → (true, null). Válido → (true, "(DD) NNNNN-NNNN"). Inválido → (false, null).
    /// </summary>
    public static (bool Valido, string? Formatado) Normalizar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
            return (true, null);
        // Letras não são aceitas (apenas dígitos, espaços, parênteses, hífen, ponto e +).
        if (texto.Any(c => !(char.IsAsciiDigit(c) || c is ' ' or '(' or ')' or '-' or '.' or '+')))
            return (false, null);

        var d = new string(texto.Where(char.IsAsciiDigit).ToArray());
        if (d.Length is 12 or 13 && d.StartsWith("55")) d = d[2..];   // +55
        if (d.Length is 11 or 12 && d.StartsWith('0')) d = d[1..];    // 0 antes do DDD

        if (Celular().IsMatch(d))
            return (true, $"({d[..2]}) {d.Substring(2, 5)}-{d.Substring(7, 4)}");
        if (Fixo().IsMatch(d))
            return (true, $"({d[..2]}) {d.Substring(2, 4)}-{d.Substring(6, 4)}");
        return (false, null);
    }
}
