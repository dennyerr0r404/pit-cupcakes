using Cupcakes.Api.Data;
using Cupcakes.Api.Dtos;
using Cupcakes.Api.Models;
using Cupcakes.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cupcakes.Api.Controllers;

/// <summary>Área administrativa: produtos e fotos (RF07), pedidos (RF08) e feedbacks (RF12). Exige perfil ADMIN (RNF02).</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/admin")]
public class AdminController(ProdutoRepositorio produtos, PedidoService pedidos, FeedbackRepositorio feedbacks) : ControllerBase
{
    // ---------------- Pedidos ----------------

    [HttpGet("pedidos")]
    public async Task<List<PedidoDto>> Pedidos([FromQuery] string? situacao, CancellationToken ct) =>
        (await pedidos.ListarParaAdminAsync(situacao, ct)).Select(p => p.ToDto(incluirCliente: true)).ToList();

    [HttpPatch("pedidos/{id:int}/status")]
    public async Task<PedidoDto> AtualizarStatus(int id, AtualizarStatusRequest req, CancellationToken ct) =>
        (await pedidos.AtualizarStatusAsync(id, req.Status, ct)).ToDto(incluirCliente: true);

    // ---------------- Feedbacks (RF12) ----------------

    /// <summary>situacao: "nao_lidos" ou vazio (todos). Inclui o resumo com as médias.</summary>
    [HttpGet("feedbacks")]
    public async Task<PainelFeedbackDto> Feedbacks([FromQuery] string? situacao, CancellationToken ct)
    {
        var (total, naoLidos, mediaProduto, mediaAtendimento) = await feedbacks.ResumoAsync(ct);
        var itens = await feedbacks.ListarParaAdminAsync(situacao, ct);
        return new PainelFeedbackDto(
            new ResumoFeedbackDto(total, naoLidos, mediaProduto, mediaAtendimento),
            itens.Select(f => f.ToDto(incluirCliente: true)).ToList());
    }

    [HttpPatch("feedbacks/{id:int}/lido")]
    public async Task<FeedbackDto> MarcarLido(int id, LidoRequest req, CancellationToken ct)
    {
        if (!await feedbacks.DefinirLidoAsync(id, req.Lido, ct))
            throw new RegraNegocioException("Feedback não encontrado.", 404);
        return (await feedbacks.BuscarAsync(id, ct))!.ToDto(incluirCliente: true);
    }

    // ---------------- Produtos ----------------

    [HttpGet("categorias")]
    public async Task<List<CategoriaDto>> Categorias(CancellationToken ct) =>
        (await produtos.ListarCategoriasAsync(somenteAtivas: false, ct)).Select(c => c.ToDto()).ToList();

    [HttpGet("produtos")]
    public async Task<List<ProdutoDto>> Produtos(CancellationToken ct) =>
        (await produtos.ListarTodosAsync(ct)).Select(p => p.ToDto()).ToList();

    [HttpGet("produtos/{id:int}")]
    public async Task<ProdutoDto> Produto(int id, CancellationToken ct) => (await Buscar(id, ct)).ToDto();

    [HttpPost("produtos")]
    public async Task<ActionResult<ProdutoDto>> CriarProduto(ProdutoSalvarRequest req, CancellationToken ct)
    {
        var produto = new Produto();
        await Preencher(produto, req, ct);
        await produtos.InserirAsync(produto, ct);
        var salvo = await Buscar(produto.IdProduto, ct);
        return CreatedAtAction(nameof(Produto), new { id = salvo.IdProduto }, salvo.ToDto());
    }

    [HttpPut("produtos/{id:int}")]
    public async Task<ProdutoDto> AlterarProduto(int id, ProdutoSalvarRequest req, CancellationToken ct)
    {
        var produto = await Buscar(id, ct);
        await Preencher(produto, req, ct);
        await produtos.AtualizarAsync(produto, ct);
        // Se o administrador trocou a foto enviada por uma ilustração ou URL, apaga a foto antiga.
        if (produto.ImagemUrl is null || !produto.ImagemUrl.StartsWith(ProdutoRepositorio.UrlImagem(id)))
            await produtos.RemoverImagemAsync(id, ct);
        return (await Buscar(id, ct)).ToDto();
    }

    /// <summary>
    /// Laudo, item 7: envio da foto do produto pelo computador (JPG, PNG ou WebP, até 2 MB).
    /// O tipo é conferido pelo conteúdo do arquivo, não pelo nome ou pelo que o navegador informa.
    /// </summary>
    [HttpPost("produtos/{id:int}/imagem")]
    [RequestSizeLimit(3 * 1024 * 1024)]
    public async Task<ProdutoDto> EnviarImagem(int id, IFormFile? arquivo, CancellationToken ct)
    {
        await Buscar(id, ct);
        if (arquivo is null || arquivo.Length == 0)
            throw new CampoInvalidoException("arquivo", "Escolha uma foto para enviar.");
        if (arquivo.Length > TamanhoMaximoImagem)
            throw new CampoInvalidoException("arquivo", "A foto deve ter no máximo 2 MB.");

        using var ms = new MemoryStream();
        await arquivo.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var tipo = DetectarTipoImagem(bytes)
                   ?? throw new CampoInvalidoException("arquivo", "Formato não aceito. Envie uma foto JPG, PNG ou WebP.");

        await produtos.SalvarImagemAsync(id, bytes, tipo, ct);
        return (await Buscar(id, ct)).ToDto();
    }

    public const int TamanhoMaximoImagem = 2 * 1024 * 1024;

    /// <summary>Identifica JPG, PNG ou WebP pelos primeiros bytes do arquivo ("assinatura").</summary>
    internal static string? DetectarTipoImagem(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return "image/jpeg";
        if (b.Length >= 8 && b.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        if (b.Length >= 12 && b.AsSpan(0, 4).SequenceEqual("RIFF"u8) && b.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";
        return null;
    }

    /// <summary>Ativa ou desativa. Produtos não são excluídos para preservar o histórico de pedidos.</summary>
    [HttpPatch("produtos/{id:int}/ativo")]
    public async Task<ProdutoDto> DefinirAtivo(int id, AtivoRequest req, CancellationToken ct)
    {
        await Buscar(id, ct);
        await produtos.DefinirAtivoAsync(id, req.Ativo, ct);
        return (await Buscar(id, ct)).ToDto();
    }

    private async Task<Produto> Buscar(int id, CancellationToken ct) =>
        await produtos.BuscarAsync(id, ct) ?? throw new RegraNegocioException("Produto não encontrado.", 404);

    private async Task Preencher(Produto p, ProdutoSalvarRequest req, CancellationToken ct)
    {
        if (!await produtos.CategoriaExisteAsync(req.CategoriaId, ct))
            throw new RegraNegocioException("Selecione uma categoria válida.");
        // Laudo, item 6: não permite dois produtos com o mesmo nome.
        if (await produtos.NomeExisteAsync(req.Nome, p.IdProduto == 0 ? null : p.IdProduto, ct))
            throw new CampoInvalidoException("nome", "Já existe um produto com este nome.", 409);
        p.Nome = req.Nome.Trim();
        p.Descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim();
        p.Preco = decimal.Round(req.Preco, 2);
        p.QuantidadeEstoque = req.QuantidadeEstoque;
        p.ImagemUrl = string.IsNullOrWhiteSpace(req.ImagemUrl) ? null : req.ImagemUrl.Trim();
        p.IdCategoria = req.CategoriaId;
        p.Ativo = req.Ativo;
    }
}
