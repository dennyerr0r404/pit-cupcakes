using Cupcakes.Api.Data;
using Cupcakes.Api.Dtos;
using Cupcakes.Api.Models;
using Cupcakes.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cupcakes.Api.Controllers;

/// <summary>Cadastro, login e logout (RF04). A sessão fica num cookie HttpOnly.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(UsuarioRepositorio usuarios, SenhaService senhas) : ControllerBase
{
    [HttpPost("cadastro")]
    public async Task<ActionResult<LoginResponse>> Cadastrar(CadastroRequest req, CancellationToken ct)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        if (await usuarios.EmailExisteAsync(email, ct))
            throw new RegraNegocioException("Este e-mail já está cadastrado. Faça login ou use outro e-mail.", 409);

        var usuario = new Usuario
        {
            Nome = req.Nome.Trim(),
            Email = email,
            Telefone = string.IsNullOrWhiteSpace(req.Telefone) ? null : req.Telefone.Trim(),
            SenhaHash = senhas.GerarHash(req.Senha),
            Perfil = PerfilUsuario.Cliente,
        };
        await usuarios.InserirAsync(usuario, ct);
        await Entrar(usuario);
        return Created("/api/auth/me", new LoginResponse(usuario.ToDto()));
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest req, CancellationToken ct)
    {
        var usuario = await usuarios.BuscarPorEmailAsync(req.Email.Trim().ToLowerInvariant(), ct);
        var (confere, regravar) = usuario is null ? (false, false) : senhas.Verificar(usuario.SenhaHash, req.Senha);
        if (usuario is null || !confere)
            throw new RegraNegocioException("E-mail ou senha incorretos.", 401);
        if (regravar)
            await usuarios.AtualizarSenhaAsync(usuario.IdUsuario, senhas.GerarHash(req.Senha), ct);

        await Entrar(usuario);
        return new LoginResponse(usuario.ToDto());
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UsuarioDto>> Me(CancellationToken ct)
    {
        var usuario = await usuarios.BuscarPorIdAsync(User.IdUsuario(), ct);
        if (usuario is null)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            throw new RegraNegocioException("Faça login para continuar.", 401);
        }
        return usuario.ToDto();
    }

    private Task Entrar(Usuario u) => HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        Services.Sessao.CriarPrincipal(u, CookieAuthenticationDefaults.AuthenticationScheme),
        new AuthenticationProperties { IsPersistent = true });
}
