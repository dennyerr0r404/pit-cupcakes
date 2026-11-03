using System.Security.Claims;
using Cupcakes.Api.Models;
using Microsoft.AspNetCore.Identity;

namespace Cupcakes.Api.Services;

/// <summary>
/// Hash de senha com PBKDF2 (HMAC-SHA512, 100 mil iterações, sal aleatório),
/// usando o PasswordHasher do ASP.NET Core Identity. A senha original nunca é armazenada.
/// </summary>
public class SenhaService
{
    private readonly PasswordHasher<Usuario> _hasher = new();

    public string GerarHash(string senha) => _hasher.HashPassword(null!, senha);

    /// <summary>Retorna se confere e se o hash deve ser regravado (algoritmo antigo).</summary>
    public (bool Confere, bool Regravar) Verificar(string hash, string senha)
    {
        if (string.IsNullOrEmpty(hash)) return (false, false);
        try
        {
            var r = _hasher.VerifyHashedPassword(null!, hash, senha);
            return (r != PasswordVerificationResult.Failed, r == PasswordVerificationResult.SuccessRehashNeeded);
        }
        catch (FormatException)
        {
            return (false, false);
        }
    }
}

public static class Sessao
{
    /// <summary>Monta a identidade gravada no cookie de autenticação.</summary>
    public static ClaimsPrincipal CriarPrincipal(Usuario u, string esquema) => new(new ClaimsIdentity(
    [
        new Claim(ClaimTypes.NameIdentifier, u.IdUsuario.ToString()),
        new Claim(ClaimTypes.Name, u.Nome),
        new Claim(ClaimTypes.Email, u.Email),
        new Claim(ClaimTypes.Role, u.Perfil.Codigo()),
    ], esquema));

    public static int IdUsuario(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());

    public static bool EhAdmin(this ClaimsPrincipal user) => user.IsInRole(PerfilUsuario.Admin.Codigo());
}
