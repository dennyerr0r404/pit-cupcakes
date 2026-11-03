using Cupcakes.Api.Dtos;
using Cupcakes.Api.Models;
using Cupcakes.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cupcakes.Api.Controllers;

/// <summary>Pedidos do cliente autenticado (RF05, RF06, RF10).</summary>
[ApiController]
[Authorize]
[Route("api/pedidos")]
public class PedidosController(PedidoService pedidos) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<PedidoDto>> Criar(CriarPedidoRequest req, CancellationToken ct)
    {
        var pedido = await pedidos.CriarAsync(User.IdUsuario(), req, ct);
        return CreatedAtAction(nameof(Buscar), new { id = pedido.IdPedido }, pedido.ToDto());
    }

    [HttpGet]
    public async Task<List<PedidoDto>> Listar(CancellationToken ct) =>
        (await pedidos.ListarDoClienteAsync(User.IdUsuario(), ct)).Select(p => p.ToDto()).ToList();

    /// <summary>O cliente só vê os próprios pedidos (RNF03); o administrador vê todos.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PedidoDto>> Buscar(int id, CancellationToken ct)
    {
        var pedido = await pedidos.BuscarAsync(id, ct);
        if (pedido is null || (pedido.IdUsuario != User.IdUsuario() && !User.EhAdmin()))
            throw new RegraNegocioException("Pedido não encontrado.", 404);
        return pedido.ToDto(incluirCliente: User.EhAdmin());
    }
}
