using System.Security.Cryptography;
using System.Text;
using Cupcakes.Api.Data;
using Cupcakes.Api.Models;
using Npgsql;

namespace Cupcakes.Api.Services;

public class RecuperacaoSenhaOptions
{
    /// <summary>
    /// Modo demonstração: o link de redefinição aparece na tela, sem envio por e-mail.
    /// Atenção: qualquer pessoa que saiba o e-mail consegue trocar a senha. Para uso real,
    /// desligue (false) e envie o link por e-mail; ele também é registrado no log do servidor.
    /// </summary>
    public bool ExibirLinkNaTela { get; set; } = true;

    /// <summary>Validade do link, em minutos.</summary>
    public int MinutosValidade { get; set; } = 30;
}

/// <summary>
/// "Esqueci minha senha" (Laudo, item 3). O código do link é aleatório (256 bits);
/// no banco fica só o seu hash SHA-256. Cada link vale uma única vez e expira.
/// </summary>
public class RecuperacaoSenhaService(
    NpgsqlDataSource db, UsuarioRepositorio usuarios, SenhaService senhas,
    RecuperacaoSenhaOptions opcoes, ILogger<RecuperacaoSenhaService> log)
{
    public RecuperacaoSenhaOptions Opcoes => opcoes;

    /// <summary>Gera o código de redefinição. Retorna null se o e-mail não estiver cadastrado.</summary>
    public async Task<string?> SolicitarAsync(string email, CancellationToken ct = default)
    {
        var usuario = await usuarios.BuscarPorEmailAsync(email.Trim().ToLowerInvariant(), ct);
        if (usuario is null)
            return null;

        var codigo = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        await using var con = await db.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);
        // Um pedido novo invalida os links anteriores ainda não usados.
        await using (var cmd = new NpgsqlCommand(
            "UPDATE redefinicao_senha SET usado_em = CURRENT_TIMESTAMP WHERE id_usuario = $1 AND usado_em IS NULL", con, tx))
        {
            cmd.Parameters.AddWithValue(usuario.IdUsuario);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = new NpgsqlCommand(
            "INSERT INTO redefinicao_senha (id_usuario, token_hash, expira_em) " +
            "VALUES ($1, $2, CURRENT_TIMESTAMP + make_interval(mins => $3))", con, tx))
        {
            cmd.Parameters.AddWithValue(usuario.IdUsuario);
            cmd.Parameters.AddWithValue(Hash(codigo));
            cmd.Parameters.AddWithValue(opcoes.MinutosValidade);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);

        log.LogInformation("Link de redefinição de senha para {Email}: /redefinir-senha.html?codigo={Codigo}", usuario.Email, codigo);
        return codigo;
    }

    /// <summary>Troca a senha usando o código do link. Lança erro se inválido, usado ou expirado.</summary>
    public async Task RedefinirAsync(string codigo, string novaSenha, CancellationToken ct = default)
    {
        await using var con = await db.OpenConnectionAsync(ct);
        await using var tx = await con.BeginTransactionAsync(ct);

        int idRedefinicao, idUsuario;
        await using (var cmd = new NpgsqlCommand(
            "SELECT id_redefinicao, id_usuario FROM redefinicao_senha " +
            "WHERE token_hash = $1 AND usado_em IS NULL AND expira_em > CURRENT_TIMESTAMP FOR UPDATE", con, tx))
        {
            cmd.Parameters.AddWithValue(Hash(codigo ?? ""));
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                throw new RegraNegocioException("Este link de redefinição é inválido, já foi usado ou expirou. Peça um novo.", 400);
            idRedefinicao = r.GetInt32(0);
            idUsuario = r.GetInt32(1);
        }

        await using (var cmd = new NpgsqlCommand("UPDATE usuario SET senha_hash = $1 WHERE id_usuario = $2", con, tx))
        {
            cmd.Parameters.AddWithValue(senhas.GerarHash(novaSenha));
            cmd.Parameters.AddWithValue(idUsuario);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await using (var cmd = new NpgsqlCommand(
            "UPDATE redefinicao_senha SET usado_em = CURRENT_TIMESTAMP WHERE id_usuario = $1 AND usado_em IS NULL", con, tx))
        {
            cmd.Parameters.AddWithValue(idUsuario);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        log.LogInformation("Senha redefinida pelo link {Id}", idRedefinicao);
    }

    internal static string Hash(string codigo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(codigo))).ToLowerInvariant();

    private static string Base64UrlEncode(byte[] b) =>
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
