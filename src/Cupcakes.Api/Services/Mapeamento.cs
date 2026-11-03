using Cupcakes.Api.Dtos;
using Cupcakes.Api.Models;

namespace Cupcakes.Api.Services;

/// <summary>Converte entidades em DTOs e define os rótulos exibidos ao usuário.</summary>
public static class Mapeamento
{
    public static string Rotulo(StatusPedido s, TipoEntrega tipo) => s switch
    {
        StatusPedido.Recebido => "Pedido recebido",
        StatusPedido.EmPreparo => "Em preparo",
        StatusPedido.ProntoParaRetirada => "Pronto para retirada",
        StatusPedido.SaiuParaEntrega => "Saiu para entrega",
        StatusPedido.Entregue => tipo == TipoEntrega.Retirada ? "Retirado" : "Entregue",
        StatusPedido.Cancelado => "Cancelado",
        _ => s.ToString(),
    };

    public static UsuarioDto ToDto(this Usuario u) => new(u.IdUsuario, u.Nome, u.Email, u.Telefone, u.Perfil.Codigo());

    public static CategoriaDto ToDto(this Categoria c) => new(c.IdCategoria, c.Nome, c.Descricao);

    public static ProdutoDto ToDto(this Produto p) => new(
        p.IdProduto, p.Nome, p.Descricao, p.Preco, p.QuantidadeEstoque,
        p.Ativo && p.QuantidadeEstoque > 0, p.ImagemUrl, p.IdCategoria, p.NomeCategoria, p.Ativo);

    public static PedidoDto ToDto(this Pedido p, bool incluirCliente = false)
    {
        var etapas = new List<EtapaDto>();
        if (p.Status == StatusPedido.Cancelado)
        {
            etapas.Add(new EtapaDto(StatusPedido.Recebido.Codigo(), Rotulo(StatusPedido.Recebido, p.TipoEntrega), true, false));
            etapas.Add(new EtapaDto(StatusPedido.Cancelado.Codigo(), Rotulo(StatusPedido.Cancelado, p.TipoEntrega), true, true));
        }
        else
        {
            var lista = FluxoStatus.Etapas(p.TipoEntrega);
            var atual = lista.ToList().IndexOf(p.Status);
            for (var i = 0; i < lista.Count; i++)
                etapas.Add(new EtapaDto(lista[i].Codigo(), Rotulo(lista[i], p.TipoEntrega), i <= atual, i == atual));
        }

        return new PedidoDto(
            p.IdPedido, p.DataPedido, p.DataAtualizacao, p.Status.Codigo(), Rotulo(p.Status, p.TipoEntrega),
            p.ValorTotal, p.TipoEntrega.Codigo(), p.EnderecoEntrega, p.FormaPagamento.Codigo(), p.Observacao,
            p.Itens.OrderBy(i => i.IdItem)
                .Select(i => new ItemPedidoDto(i.IdProduto, i.NomeProduto, i.ImagemUrl,
                    i.Quantidade, i.PrecoUnitario, i.CalcularSubtotal()))
                .ToList(),
            etapas,
            incluirCliente ? p.NomeCliente : null,
            incluirCliente ? p.EmailCliente : null,
            incluirCliente ? p.TelefoneCliente : null,
            incluirCliente ? FluxoStatus.Proximos(p).Select(s => s.Codigo()).ToList() : []);
    }
}
