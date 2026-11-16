using Cupcakes.Api.Data;
using Cupcakes.Api.Dtos;
using Cupcakes.Api.Models;
using Cupcakes.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cupcakes.Api.Controllers;

/// <summary>Cadastro, login, logout, perfil e senha (RF04). A sessão fica num cookie HttpOnly.</summary>
[ApiController]
[Route("api/auth")]
public class AuthController(UsuarioRepositorio usuarios, SenhaService senhas, RecuperacaoSenhaService recuperacao) : ControllerBase
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
            Telefone = ValidarTelefone(req.Telefone),
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

    /// <summary>Laudo, item 4: o cliente altera nome e telefone. O e-mail não muda (é o login).</summary>
    [Authorize]
    [HttpPut("perfil")]
    public async Task<ActionResult<LoginResponse>> AtualizarPerfil(PerfilRequest req, CancellationToken ct)
    {
        var usuario = await usuarios.BuscarPorIdAsync(User.IdUsuario(), ct)
                      ?? throw new RegraNegocioException("Faça login para continuar.", 401);
        usuario.Nome = req.Nome.Trim();
        usuario.Telefone = ValidarTelefone(req.Telefone);
        await usuarios.AtualizarPerfilAsync(usuario.IdUsuario, usuario.Nome, usuario.Telefone, ct);
        await Entrar(usuario); // atualiza o nome guardado na sessão
        return new LoginResponse(usuario.ToDto());
    }

    /// <summary>Laudo, item 4: troca de senha, confirmando a senha atual.</summary>
    [Authorize]
    [HttpPost("senha")]
    public async Task<IActionResult> TrocarSenha(TrocarSenhaRequest req, CancellationToken ct)
    {
        var usuario = await usuarios.BuscarPorIdAsync(User.IdUsuario(), ct)
                      ?? throw new RegraNegocioException("Faça login para continuar.", 401);
        if (!senhas.Verificar(usuario.SenhaHash, req.SenhaAtual).Confere)
            throw new CampoInvalidoException("senhaAtual", "A senha atual está incorreta.");
        if (req.NovaSenha == req.SenhaAtual)
            throw new CampoInvalidoException("novaSenha", "A nova senha deve ser diferente da atual.");
        await usuarios.AtualizarSenhaAsync(usuario.IdUsuario, senhas.GerarHash(req.NovaSenha), ct);
        return NoContent();
    }

    /// <summary>Laudo, item 3: "Esqueci minha senha" — gera um link de redefinição.</summary>
    [HttpPost("esqueci-senha")]
    public async Task<EsqueciSenhaResponse> EsqueciSenha(EsqueciSenhaRequest req, CancellationToken ct)
    {
        var codigo = await recuperacao.SolicitarAsync(req.Email, ct);
        if (!recuperacao.Opcoes.ExibirLinkNaTela)
            // Modo seguro: mesma resposta exista ou não o e-mail; o link segue por e-mail/log.
            return new EsqueciSenhaResponse("Se o e-mail estiver cadastrado, enviaremos um link para criar uma nova senha.", null);
        if (codigo is null)
            throw new CampoInvalidoException("email", "Não encontramos uma conta com este e-mail.", 404);
        return new EsqueciSenhaResponse(
            $"Link criado. Ele vale por {recuperacao.Opcoes.MinutosValidade} minutos e pode ser usado uma única vez.",
            $"/redefinir-senha.html?codigo={codigo}");
    }

    /// <summary>Laudo, item 3: cria a nova senha a partir do link.</summary>
    [HttpPost("redefinir-senha")]
    public async Task<IActionResult> RedefinirSenha(RedefinirSenhaRequest req, CancellationToken ct)
    {
        await recuperacao.RedefinirAsync(req.Codigo, req.NovaSenha, ct);
        return NoContent();
    }

    private static string? ValidarTelefone(string? telefone)
    {
        var (valido, formatado) = Telefone.Normalizar(telefone);
        if (!valido)
            throw new CampoInvalidoException("telefone", Telefone.MensagemInvalido);
        return formatado;
    }

    private Task Entrar(Usuario u) => HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        Services.Sessao.CriarPrincipal(u, CookieAuthenticationDefaults.AuthenticationScheme),
        new AuthenticationProperties { IsPersistent = true });
}
