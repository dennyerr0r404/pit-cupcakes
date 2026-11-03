using Cupcakes.Api.Data;
using Cupcakes.Api.Infra;
using Cupcakes.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Cupcakes.Api;

/// <summary>
/// Configuração da aplicação: serviços (injeção de dependência), autenticação,
/// controladores MVC, arquivos estáticos do front-end e inicialização do banco.
/// </summary>
public static class App
{
    /// <param name="configurar">Ajustes extras (usado pelos testes de integração).</param>
    public static async Task<WebApplication> CriarAsync(string[] args, Action<WebApplicationBuilder>? configurar = null)
    {
        var builder = WebApplication.CreateBuilder(args);
        configurar?.Invoke(builder);

        // ---------- Banco de dados (Npgsql) ----------
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(Conexao.Resolver(builder.Configuration)));
        builder.Services.AddSingleton<UsuarioRepositorio>();
        builder.Services.AddSingleton<ProdutoRepositorio>();
        builder.Services.AddSingleton<PedidoRepositorio>();
        builder.Services.AddSingleton<SenhaService>();
        builder.Services.AddScoped<PedidoService>();

        // ---------- Autenticação por cookie ----------
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = "cupcakes.sessao";
                o.Cookie.HttpOnly = true;                       // inacessível ao JavaScript
                o.Cookie.SameSite = SameSiteMode.Lax;           // não é enviado em POST vindo de outros sites
                o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                o.ExpireTimeSpan = TimeSpan.FromHours(8);
                o.SlidingExpiration = true;
                // Na API, responder 401/403 em JSON em vez de redirecionar para uma página de login.
                o.Events.OnRedirectToLogin = ctx => Erros.Escrever(ctx.Response, 401, "Faça login para continuar.");
                o.Events.OnRedirectToAccessDenied = ctx => Erros.Escrever(ctx.Response, 403, "Você não tem permissão para acessar esta área.");
            });
        builder.Services.AddAuthorization();

        // ---------- MVC (controladores da API) ----------
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(App).Assembly) // controladores deste projeto (também quando hospedado pelos testes)
            .AddJsonOptions(o => o.JsonSerializerOptions.Encoder = Erros.Json.Encoder)
            .ConfigureApiBehaviorOptions(o =>
            {
                // Erros de validação no formato { mensagem, erros }
                o.InvalidModelStateResponseFactory = ctx =>
                {
                    var erros = ctx.ModelState
                        .Where(kv => kv.Value!.Errors.Count > 0)
                        .ToDictionary(kv => Erros.Campo(kv.Key), kv => kv.Value!.Errors
                            .Select(e => string.IsNullOrWhiteSpace(e.ErrorMessage) || e.Exception is not null
                                ? "Valor inválido." : e.ErrorMessage).ToArray());
                    var primeira = erros.Values.SelectMany(v => v).FirstOrDefault() ?? "Dados inválidos.";
                    return new BadRequestObjectResult(new { mensagem = primeira, erros });
                };
            });

        // Atrás de proxy (hospedagem em nuvem), respeita o protocolo/IP originais.
        builder.Services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            o.KnownNetworks.Clear();
            o.KnownProxies.Clear();
        });

        var app = builder.Build();

        app.UseForwardedHeaders();
        app.UseMiddleware<TratamentoErrosMiddleware>();
        app.UseDefaultFiles();
        app.UseStaticFiles(new StaticFileOptions
        {
            // Sempre revalidar HTML/JS/CSS, para o navegador não usar versões antigas após uma atualização.
            OnPrepareResponse = ctx => ctx.Context.Response.Headers.CacheControl = "no-cache",
        });
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapGet("/api/saude", () => Results.Ok(new { status = "ok" }));
        app.Map("/api/{**resto}", () => Results.NotFound(new { mensagem = "Recurso não encontrado." }));

        if (app.Configuration.GetValue("Banco:InicializarAutomaticamente", true))
        {
            var db = app.Services.GetRequiredService<NpgsqlDataSource>();
            await InicializadorBanco.GarantirCriadoAsync(db, app.Logger);
        }

        return app;
    }
}
