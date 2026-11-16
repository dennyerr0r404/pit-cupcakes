using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Cupcakes.Api.Controllers;
using Cupcakes.Api.Models;
using Npgsql;
using Xunit;

namespace Cupcakes.Tests;

/// <summary>Regras das melhorias do Laudo de Qualidade que não dependem do banco.</summary>
public class MelhoriasLaudoDominioTests
{
    [Theory]
    [InlineData("11987654321", "(11) 98765-4321")]
    [InlineData("(11) 98765-4321", "(11) 98765-4321")]
    [InlineData("11 3333-4444", "(11) 3333-4444")]
    [InlineData("+55 21 99876-5432", "(21) 99876-5432")]
    [InlineData("011 3333-4444", "(11) 3333-4444")]
    public void Telefone_Valido_EhFormatado(string entrada, string esperado)
    {
        var (valido, formatado) = Telefone.Normalizar(entrada);
        Assert.True(valido);
        Assert.Equal(esperado, formatado);
    }

    [Theory]
    [InlineData("11abc")]              // letras
    [InlineData("1198")]               // incompleto
    [InlineData("(00) 98765-4321")]    // DDD inválido
    [InlineData("(11) 88765-4321")]    // 9 dígitos sem começar com 9
    [InlineData("(11) 1333-4444")]     // fixo começando com 1
    [InlineData("119876543210")]       // dígitos demais
    public void Telefone_Invalido_EhRecusado(string entrada) =>
        Assert.False(Telefone.Normalizar(entrada).Valido);

    [Fact]
    public void Telefone_Vazio_EhAceito() => Assert.Equal((true, (string?)null), Telefone.Normalizar("  "));

    [Fact]
    public void Imagem_TipoDetectadoPeloConteudo()
    {
        Assert.Equal("image/jpeg", AdminController.DetectarTipoImagem([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal("image/png", AdminController.DetectarTipoImagem(ImagemPng()));
        Assert.Equal("image/webp", AdminController.DetectarTipoImagem("RIFF\0\0\0\0WEBPVP8 "u8.ToArray()));
        Assert.Null(AdminController.DetectarTipoImagem("GIF89a......"u8.ToArray()));
        Assert.Null(AdminController.DetectarTipoImagem("<svg></svg>"u8.ToArray()));
    }

    /// <summary>PNG válido de 1×1 pixel.</summary>
    internal static byte[] ImagemPng() => Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8DwHwAFBQIAX8jx0gAAAABJRU5ErkJggg==");
}

/// <summary>Melhorias do Laudo pela API, com aplicação e PostgreSQL reais (CT18 a CT23).</summary>
public class MelhoriasLaudoApiTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private record Usuario(int Id, string Nome, string Email, string? Telefone);
    private record Login(Usuario Usuario);
    private record Esqueci(string Mensagem, string? Link);
    private record Produto(int Id, string Nome, string? ImagemUrl);
    private record Erro(string Mensagem, Dictionary<string, string[]>? Erros);

    private static async Task<Erro> LerErro(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<Erro>(Json))!;
    private static string EmailNovo() => $"laudo-{Guid.NewGuid():N}@teste.com";

    // ---------------- Item 2: telefone ----------------

    [Fact(DisplayName = "CT18 - Telefone inválido é recusado e o válido é gravado no formato padrão")]
    public async Task CT18_Telefone()
    {
        var http = api.NovoCliente();
        var ruim = await http.PostAsJsonAsync("/api/auth/cadastro", new { nome = "Teste Fone", email = EmailNovo(), telefone = "11abc", senha = "segredo1" });
        Assert.Equal(HttpStatusCode.BadRequest, ruim.StatusCode);
        Assert.True((await LerErro(ruim)).Erros!.ContainsKey("telefone"));

        var ok = await http.PostAsJsonAsync("/api/auth/cadastro", new { nome = "Teste Fone", email = EmailNovo(), telefone = "11987654321", senha = "segredo1" });
        Assert.Equal(HttpStatusCode.Created, ok.StatusCode);
        Assert.Equal("(11) 98765-4321", (await ok.Content.ReadFromJsonAsync<Login>(Json))!.Usuario.Telefone);
    }

    // ---------------- Item 4: perfil ----------------

    [Fact(DisplayName = "CT19 - Cliente edita nome e telefone e troca a senha confirmando a atual")]
    public async Task CT19_EditarPerfilETrocarSenha()
    {
        var email = EmailNovo();
        var http = api.NovoCliente();
        await http.PostAsJsonAsync("/api/auth/cadastro", new { nome = "Nome Antigo", email, senha = "segredo1" });

        var perfil = await http.PutAsJsonAsync("/api/auth/perfil", new { nome = "  Nome Novo  ", telefone = "21 3333-4444" });
        Assert.Equal(HttpStatusCode.OK, perfil.StatusCode);
        var u = (await perfil.Content.ReadFromJsonAsync<Login>(Json))!.Usuario;
        Assert.Equal("Nome Novo", u.Nome);
        Assert.Equal("(21) 3333-4444", u.Telefone);
        Assert.Equal(email, u.Email);   // e-mail não muda
        Assert.Equal("Nome Novo", (await http.GetFromJsonAsync<Usuario>("/api/auth/me", Json))!.Nome);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await http.PutAsJsonAsync("/api/auth/perfil", new { nome = "Nome Novo", telefone = "123" })).StatusCode);

        var errada = await http.PostAsJsonAsync("/api/auth/senha", new { senhaAtual = "errada", novaSenha = "novaSenha1" });
        Assert.Equal(HttpStatusCode.BadRequest, errada.StatusCode);
        Assert.True((await LerErro(errada)).Erros!.ContainsKey("senhaAtual"));

        Assert.Equal(HttpStatusCode.NoContent,
            (await http.PostAsJsonAsync("/api/auth/senha", new { senhaAtual = "segredo1", novaSenha = "novaSenha1" })).StatusCode);
        var login = api.NovoCliente();
        Assert.Equal(HttpStatusCode.Unauthorized, (await login.PostAsJsonAsync("/api/auth/login", new { email, senha = "segredo1" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await login.PostAsJsonAsync("/api/auth/login", new { email, senha = "novaSenha1" })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await api.NovoCliente().PutAsJsonAsync("/api/auth/perfil", new { nome = "Invasor" })).StatusCode);
    }

    // ---------------- Item 3: esqueci minha senha ----------------

    private static string Codigo(string link) => link[(link.IndexOf("codigo=", StringComparison.Ordinal) + 7)..];

    [Fact(DisplayName = "CT20 - Esqueci minha senha: link de uso único redefine a senha")]
    public async Task CT20_RecuperarSenha()
    {
        var email = EmailNovo();
        await api.NovoCliente().PostAsJsonAsync("/api/auth/cadastro", new { nome = "Esquecido", email, senha = "segredo1" });
        var http = api.NovoCliente();

        Assert.Equal(HttpStatusCode.NotFound,
            (await http.PostAsJsonAsync("/api/auth/esqueci-senha", new { email = "ninguem@teste.com" })).StatusCode);

        var r = await http.PostAsJsonAsync("/api/auth/esqueci-senha", new { email = email.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var link = (await r.Content.ReadFromJsonAsync<Esqueci>(Json))!.Link!;
        Assert.StartsWith("/redefinir-senha.html?codigo=", link);

        // O código não fica gravado no banco, só o seu hash.
        await using (var con = new NpgsqlConnection(api.ConnectionString))
        {
            await con.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM redefinicao_senha WHERE token_hash = $1", con);
            cmd.Parameters.AddWithValue(Codigo(link));
            Assert.Equal(0L, (long)(await cmd.ExecuteScalarAsync())!);
        }

        Assert.Equal(HttpStatusCode.NoContent,
            (await http.PostAsJsonAsync("/api/auth/redefinir-senha", new { codigo = Codigo(link), novaSenha = "recuperada1" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await http.PostAsJsonAsync("/api/auth/login", new { email, senha = "recuperada1" })).StatusCode);

        var reuso = await http.PostAsJsonAsync("/api/auth/redefinir-senha", new { codigo = Codigo(link), novaSenha = "outra1234" });
        Assert.Equal(HttpStatusCode.BadRequest, reuso.StatusCode);
        Assert.Contains("já foi usado", (await LerErro(reuso)).Mensagem);
    }

    [Fact(DisplayName = "CT20b - Link expirado ou substituído por um novo pedido não funciona")]
    public async Task CT20b_LinkExpiradoOuSubstituido()
    {
        var email = EmailNovo();
        await api.NovoCliente().PostAsJsonAsync("/api/auth/cadastro", new { nome = "Esquecido Dois", email, senha = "segredo1" });
        var http = api.NovoCliente();

        var primeiro = (await (await http.PostAsJsonAsync("/api/auth/esqueci-senha", new { email })).Content.ReadFromJsonAsync<Esqueci>(Json))!.Link!;
        var segundo = (await (await http.PostAsJsonAsync("/api/auth/esqueci-senha", new { email })).Content.ReadFromJsonAsync<Esqueci>(Json))!.Link!;
        Assert.Equal(HttpStatusCode.BadRequest,
            (await http.PostAsJsonAsync("/api/auth/redefinir-senha", new { codigo = Codigo(primeiro), novaSenha = "nova12345" })).StatusCode);

        await using (var con = new NpgsqlConnection(api.ConnectionString))
        {
            await con.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE redefinicao_senha SET criado_em = criado_em - interval '2 hours', expira_em = CURRENT_TIMESTAMP - interval '1 minute' " +
                "WHERE id_usuario = (SELECT id_usuario FROM usuario WHERE email = $1)", con);
            cmd.Parameters.AddWithValue(email);
            await cmd.ExecuteNonQueryAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest,
            (await http.PostAsJsonAsync("/api/auth/redefinir-senha", new { codigo = Codigo(segundo), novaSenha = "nova12345" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await http.PostAsJsonAsync("/api/auth/login", new { email, senha = "segredo1" })).StatusCode);
    }

    // ---------------- Item 6: nome único ----------------

    [Fact(DisplayName = "CT21 - Produto com nome repetido é recusado (maiúsculas e espaços não importam)")]
    public async Task CT21_NomeUnico()
    {
        var admin = await api.AdminAsync();
        var nome = $"Único {Guid.NewGuid():N}"[..20];
        var criado = await admin.PostAsJsonAsync("/api/admin/produtos", new { nome, preco = 10, quantidadeEstoque = 1, categoriaId = 1 });
        Assert.Equal(HttpStatusCode.Created, criado.StatusCode);
        var p = (await criado.Content.ReadFromJsonAsync<Produto>(Json))!;

        var repetido = await admin.PostAsJsonAsync("/api/admin/produtos",
            new { nome = "  " + nome.ToUpperInvariant() + " ", preco = 10, quantidadeEstoque = 1, categoriaId = 1 });
        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
        Assert.True((await LerErro(repetido)).Erros!.ContainsKey("nome"));

        // Alterar o próprio produto mantendo o nome é permitido; usar o nome de outro, não.
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/admin/produtos/{p.Id}",
            new { nome, preco = 11, quantidadeEstoque = 2, categoriaId = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/admin/produtos/{p.Id}",
            new { nome = "Velvet Red", preco = 11, quantidadeEstoque = 2, categoriaId = 1 })).StatusCode);
    }

    // ---------------- Item 7: foto do produto ----------------

    private static MultipartFormDataContent Arquivo(byte[] conteudo, string nome, string tipo)
    {
        var form = new MultipartFormDataContent();
        var arq = new ByteArrayContent(conteudo);
        arq.Headers.ContentType = new MediaTypeHeaderValue(tipo);
        form.Add(arq, "arquivo", nome);
        return form;
    }

    [Fact(DisplayName = "CT22 - Administrador envia foto do produto, que fica no banco e aparece no catálogo")]
    public async Task CT22_EnviarFoto()
    {
        var admin = await api.AdminAsync();
        var p = (await (await admin.PostAsJsonAsync("/api/admin/produtos",
            new { nome = $"Foto {Guid.NewGuid():N}"[..18], preco = 10, quantidadeEstoque = 3, categoriaId = 1, imagemUrl = "/img/baunilha.svg" }))
            .Content.ReadFromJsonAsync<Produto>(Json))!;
        var png = MelhoriasLaudoDominioTests.ImagemPng();

        var r = await admin.PostAsync($"/api/admin/produtos/{p.Id}/imagem", Arquivo(png, "foto.png", "image/png"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var url = (await r.Content.ReadFromJsonAsync<Produto>(Json))!.ImagemUrl!;
        Assert.StartsWith($"/api/produtos/{p.Id}/imagem?v=", url);

        var foto = await api.NovoCliente().GetAsync(url);   // pública, como as demais imagens
        Assert.Equal(HttpStatusCode.OK, foto.StatusCode);
        Assert.Equal("image/png", foto.Content.Headers.ContentType!.MediaType);
        Assert.Equal(png, await foto.Content.ReadAsByteArrayAsync());

        var catalogo = await api.NovoCliente().GetFromJsonAsync<List<Produto>>("/api/produtos", Json);
        Assert.Equal(url, catalogo!.Single(x => x.Id == p.Id).ImagemUrl);

        // Trocar por uma ilustração apaga a foto enviada.
        await admin.PutAsJsonAsync($"/api/admin/produtos/{p.Id}",
            new { nome = p.Nome, preco = 10, quantidadeEstoque = 3, categoriaId = 1, imagemUrl = "/img/pistache.svg" });
        Assert.Equal(HttpStatusCode.NotFound, (await api.NovoCliente().GetAsync($"/api/produtos/{p.Id}/imagem")).StatusCode);
    }

    [Fact(DisplayName = "CT23 - Foto inválida, grande demais ou enviada por cliente é recusada")]
    public async Task CT23_FotoInvalida()
    {
        var admin = await api.AdminAsync();
        var falsa = await admin.PostAsync("/api/admin/produtos/1/imagem", Arquivo("isto não é imagem"u8.ToArray(), "x.png", "image/png"));
        Assert.Equal(HttpStatusCode.BadRequest, falsa.StatusCode);
        Assert.Contains("Formato não aceito", (await LerErro(falsa)).Mensagem);

        var grande = new byte[2 * 1024 * 1024 + 1];
        grande[0] = 0xFF; grande[1] = 0xD8; grande[2] = 0xFF;
        var r = await admin.PostAsync("/api/admin/produtos/1/imagem", Arquivo(grande, "g.jpg", "image/jpeg"));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        Assert.Contains("2 MB", (await LerErro(r)).Mensagem);

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/admin/produtos/1/imagem", new MultipartFormDataContent())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync("/api/admin/produtos/99999/imagem",
            Arquivo(MelhoriasLaudoDominioTests.ImagemPng(), "f.png", "image/png"))).StatusCode);

        var cliente = await api.ClienteLogadoAsync("cliente@teste.com", ApiFixture.SenhaCliente);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsync("/api/admin/produtos/1/imagem",
            Arquivo(MelhoriasLaudoDominioTests.ImagemPng(), "f.png", "image/png"))).StatusCode);
    }
}
