using Cupcakes.Api.Data;
using Cupcakes.Api.Dtos;
using Cupcakes.Api.Models;
using Cupcakes.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cupcakes.Api.Controllers;

/// <summary>Área administrativa: produtos (RF07) e pedidos (RF08). Exige perfil ADMIN (RNF02).</summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/admin")]
public class AdminController(ProdutoRepositorio produtos, PedidoService pedidos) : ControllerBase
{
    // ---------------- Pedidos ----------------

    [HttpGet("pedidos")]
    public async Task<List<PedidoDto>> Pedidos([FromQuery] string? situacao, CancellationToken ct) =>
        (await pedidos.ListarParaAdminAsync(situacao, ct)).Select(p => p.ToDto(incluirCliente: true)).ToList();

    [HttpPatch("pedidos/{id:int}/status")]
    public async Task<PedidoDto> AtualizarStatus(int id, AtualizarStatusRequest req, CancellationToken ct) =>
        (await pedidos.AtualizarStatusAsync(id, req.Status, ct)).ToDto(incluirCliente: true);

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
        return (await Buscar(id, ct)).ToDto();
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
        p.Nome = req.Nome.Trim();
        p.Descricao = string.IsNullOrWhiteSpace(req.Descricao) ? null : req.Descricao.Trim();
        p.Preco = decimal.Round(req.Preco, 2);
        p.QuantidadeEstoque = req.QuantidadeEstoque;
        p.ImagemUrl = string.IsNullOrWhiteSpace(req.ImagemUrl) ? null : req.ImagemUrl.Trim();
        p.IdCategoria = req.CategoriaId;
        p.Ativo = req.Ativo;
    }
}
