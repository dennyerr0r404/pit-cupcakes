using System.Net;
using System.Net.Http.Json;
using Cupcakes.Api;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Cupcakes.Tests;

/// <summary>
/// Sobe a aplicação de verdade (Kestrel em porta livre) ligada a um banco PostgreSQL de testes,
/// recriado do zero com os scripts de database/ a cada execução.
///
/// Conexão: variável de ambiente CUPCAKES_TEST_DB ou, por padrão,
/// Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=cupcakes_teste
/// </summary>
public class ApiFixture : IAsyncLifetime
{
    public const string SenhaAdmin = "Admin@123";
    public const string SenhaCliente = "Cliente@123";

    private WebApplication? _app;
    public Uri BaseUri { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var conexao = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("CUPCAKES_TEST_DB")
            ?? "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=cupcakes_teste");
        await RecriarBancoAsync(conexao);

        // Raiz de conteúdo = pasta do projeto da API (para servir o front-end em wwwroot).
        _app = await App.CriarAsync(["--contentRoot", PastaDaApi()], b =>
        {
            b.Configuration["ConnectionStrings:Cupcakes"] = conexao.ConnectionString;
            b.Configuration["DATABASE_URL"] = "";
            b.WebHost.UseUrls("http://127.0.0.1:0");
            b.Logging.ClearProviders();
        });
        await _app.StartAsync();
        var endereco = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        BaseUri = new Uri(endereco);
    }

    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }

    private static string PastaDaApi()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        {
            var candidata = Path.Combine(d.FullName, "src", "Cupcakes.Api");
            if (Directory.Exists(Path.Combine(candidata, "wwwroot"))) return candidata;
        }
        return AppContext.BaseDirectory;
    }

    /// <summary>Cria o banco de testes se não existir e apaga todas as tabelas.</summary>
    private static async Task RecriarBancoAsync(NpgsqlConnectionStringBuilder conexao)
    {
        var admin = new NpgsqlConnectionStringBuilder(conexao.ConnectionString) { Database = "postgres", Pooling = false };
        await using (var con = new NpgsqlConnection(admin.ConnectionString))
        {
            await con.OpenAsync();
            await using var existe = new NpgsqlCommand("SELECT 1 FROM pg_database WHERE datname = $1", con);
            existe.Parameters.AddWithValue(conexao.Database!);
            if (await existe.ExecuteScalarAsync() is null)
            {
                await using var criar = new NpgsqlCommand($"CREATE DATABASE \"{conexao.Database}\"", con);
                await criar.ExecuteNonQueryAsync();
            }
        }
        await using (var con = new NpgsqlConnection(conexao.ConnectionString))
        {
            await con.OpenAsync();
            await using var limpar = new NpgsqlCommand("DROP SCHEMA public CASCADE; CREATE SCHEMA public;", con);
            await limpar.ExecuteNonQueryAsync();
        }
        NpgsqlConnection.ClearAllPools();
    }

    /// <summary>Cliente HTTP com cookies próprios (cada um é um "navegador").</summary>
    public HttpClient NovoCliente() =>
        new(new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true }) { BaseAddress = BaseUri };

    public async Task<HttpClient> ClienteLogadoAsync(string email, string senha)
    {
        var http = NovoCliente();
        var resp = await http.PostAsJsonAsync("/api/auth/login", new { email, senha });
        resp.EnsureSuccessStatusCode();
        return http;
    }

    public Task<HttpClient> AdminAsync() => ClienteLogadoAsync("admin@cupcakes.com", SenhaAdmin);

    /// <summary>Cadastra um cliente novo (e-mail único) e devolve o cliente HTTP já logado.</summary>
    public async Task<HttpClient> NovoClienteCadastradoAsync()
    {
        var http = NovoCliente();
        var resp = await http.PostAsJsonAsync("/api/auth/cadastro", new
        {
            nome = "Cliente Teste",
            email = $"cliente-{Guid.NewGuid():N}@teste.com",
            senha = "segredo123",
        });
        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        return http;
    }
}
