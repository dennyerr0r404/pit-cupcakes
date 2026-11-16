using Cupcakes.Api.Data;
using Cupcakes.Api.Dtos;
using Cupcakes.Api.Models;
using Cupcakes.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Cupcakes.Api.Controllers;

/// <summary>Catálogo público: categorias e produtos ativos (RF01, RF02, RF11).</summary>
[ApiController]
[Route("api")]
public class CatalogoController(ProdutoRepositorio produtos) : ControllerBase
{
    [HttpGet("categorias")]
    public async Task<List<CategoriaDto>> Categorias(CancellationToken ct) =>
        (await produtos.ListarCategoriasAsync(somenteAtivas: true, ct)).Select(c => c.ToDto()).ToList();

    /// <summary>Lista os produtos ativos; filtro opcional por categoria (RF11).</summary>
    [HttpGet("produtos")]
    public async Task<List<ProdutoDto>> Produtos([FromQuery] int? categoriaId, CancellationToken ct) =>
        (await produtos.ListarCatalogoAsync(categoriaId, ct)).Select(p => p.ToDto()).ToList();

    [HttpGet("produtos/{id:int}")]
    public async Task<ProdutoDto> Produto(int id, CancellationToken ct)
    {
        var p = await produtos.BuscarAsync(id, ct);
        if (p is null || !p.Ativo)
            throw new RegraNegocioException("Este produto não está disponível.", 404);
        return p.ToDto();
    }

    /// <summary>Foto enviada pelo administrador (Laudo, item 7), guardada no banco.</summary>
    [HttpGet("produtos/{id:int}/imagem")]
    public async Task<IActionResult> Imagem(int id, CancellationToken ct)
    {
        var imagem = await produtos.BuscarImagemAsync(id, ct);
        if (imagem is null)
            return NotFound();
        // A URL muda a cada nova foto (?v=...), então o navegador pode guardar em cache.
        Response.Headers.CacheControl = "public, max-age=86400";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(imagem.Value.Conteudo, imagem.Value.TipoMime);
    }
}
