using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Cupcakes.Api.Models;
using Npgsql;

namespace Cupcakes.Api.Infra;

/// <summary>Converte exceções em respostas JSON com uma mensagem legível para o usuário.</summary>
public class TratamentoErrosMiddleware(RequestDelegate next, ILogger<TratamentoErrosMiddleware> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (RegraNegocioException ex)
        {
            await Erros.Escrever(ctx.Response, ex.StatusCode, ex.Message);
        }
        catch (PostgresException pg)
        {
            // Restrições do banco (CHECK, UNIQUE, FK) como segunda linha de defesa.
            log.LogWarning(pg, "Restrição do banco violada: {Restricao}", pg.ConstraintName);
            var (status, msg) = pg.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation => (409, "Registro duplicado."),
                PostgresErrorCodes.ForeignKeyViolation => (409, "Operação não permitida: o registro está em uso."),
                PostgresErrorCodes.CheckViolation => (400, "Dados inválidos."),
                _ => (400, "Não foi possível salvar os dados."),
            };
            await Erros.Escrever(ctx.Response, status, msg);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Erro inesperado");
            await Erros.Escrever(ctx.Response, 500, "Ocorreu um erro inesperado. Tente novamente.");
        }
    }
}

public static class Erros
{
    /// <summary>JSON com acentos legíveis (sem \u00E7 etc.).</summary>
    public static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };

    public static async Task Escrever(HttpResponse resp, int status, string mensagem)
    {
        if (resp.HasStarted) return;
        resp.StatusCode = status;
        resp.ContentType = "application/json; charset=utf-8";
        await resp.WriteAsync(JsonSerializer.Serialize(new { mensagem }, Json));
    }

    /// <summary>"$.itens[0].quantidade" / "Itens[0].Quantidade" -> "itens[0].quantidade"</summary>
    public static string Campo(string chave)
    {
        var c = chave.StartsWith("$.") ? chave[2..] : chave;
        return string.Join('.', c.Split('.').Select(p => p.Length > 0 ? char.ToLowerInvariant(p[0]) + p[1..] : p));
    }
}

/// <summary>
/// Lê a conexão de ConnectionStrings:Cupcakes ou da variável DATABASE_URL
/// (formato postgres://usuario:senha@host:porta/banco, usado por serviços de hospedagem).
/// </summary>
public static class Conexao
{
    public static string Resolver(IConfiguration config)
    {
        var url = config["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(url) && (url.StartsWith("postgres://") || url.StartsWith("postgresql://")))
        {
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':', 2);
            var csb = new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Database = uri.AbsolutePath.Trim('/'),
                Username = Uri.UnescapeDataString(userInfo[0]),
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
                SslMode = uri.Query.Contains("sslmode=disable") ? SslMode.Disable : SslMode.Require,
            };
            return csb.ConnectionString;
        }
        return config.GetConnectionString("Cupcakes")
               ?? throw new InvalidOperationException("Configure ConnectionStrings:Cupcakes ou DATABASE_URL.");
    }
}
