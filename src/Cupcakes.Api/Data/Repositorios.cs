using Cupcakes.Api.Models;
using Npgsql;

namespace Cupcakes.Api.Data;

// =====================================================================
// Camada de acesso a dados (repositórios) com SQL explícito via Npgsql.
// Os nomes de tabelas e colunas seguem database/01_schema.sql.
// =====================================================================

public class UsuarioRepositorio(NpgsqlDataSource db)
{
    private const string Colunas = "id_usuario, nome, email, telefone, senha_hash, perfil, data_cadastro";

    public async Task<Usuario?> BuscarPorEmailAsync(string email, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand($"SELECT {Colunas} FROM usuario WHERE email = $1");
        cmd.Parameters.AddWithValue(email);
        return await LerUmAsync(cmd, ct);
    }

    public async Task<Usuario?> BuscarPorIdAsync(int id, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand($"SELECT {Colunas} FROM usuario WHERE id_usuario = $1");
        cmd.Parameters.AddWithValue(id);
        return await LerUmAsync(cmd, ct);
    }

    public async Task<bool> EmailExisteAsync(string email, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand("SELECT EXISTS (SELECT 1 FROM usuario WHERE email = $1)");
        cmd.Parameters.AddWithValue(email);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task InserirAsync(Usuario u, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand(
            "INSERT INTO usuario (nome, email, telefone, senha_hash, perfil) VALUES ($1, $2, $3, $4, $5) " +
            "RETURNING id_usuario, data_cadastro");
        cmd.Parameters.AddWithValue(u.Nome);
        cmd.Parameters.AddWithValue(u.Email);
        cmd.Parameters.AddWithValue((object?)u.Telefone ?? DBNull.Value);
        cmd.Parameters.AddWithValue(u.SenhaHash);
        cmd.Parameters.AddWithValue(u.Perfil.Codigo());
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        u.IdUsuario = r.GetInt32(0);
        u.DataCadastro = r.GetDateTime(1);
    }

    public async Task AtualizarSenhaAsync(int id, string hash, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand("UPDATE usuario SET senha_hash = $1 WHERE id_usuario = $2");
        cmd.Parameters.AddWithValue(hash);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<Usuario?> LerUmAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return null;
        return new Usuario
        {
            IdUsuario = r.GetInt32(0),
            Nome = r.GetString(1),
            Email = r.GetString(2),
            Telefone = r.IsDBNull(3) ? null : r.GetString(3),
            SenhaHash = r.GetString(4),
            Perfil = Codigos.ParsePerfil(r.GetString(5)),
            DataCadastro = r.GetDateTime(6),
        };
    }
}

public class ProdutoRepositorio(NpgsqlDataSource db)
{
    internal const string SelectProduto =
        "SELECT p.id_produto, p.id_categoria, c.nome, p.nome, p.descricao, p.preco, p.quantidade_estoque, " +
        "p.imagem_url, p.ativo, p.data_cadastro FROM produto p JOIN categoria c ON c.id_categoria = p.id_categoria";

    public async Task<List<Categoria>> ListarCategoriasAsync(bool somenteAtivas, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand(
            "SELECT id_categoria, nome, descricao, ordem_exibicao, ativo FROM categoria " +
            (somenteAtivas ? "WHERE ativo " : "") + "ORDER BY ordem_exibicao, nome");
        var lista = new List<Categoria>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new Categoria
            {
                IdCategoria = r.GetInt32(0),
                Nome = r.GetString(1),
                Descricao = r.IsDBNull(2) ? null : r.GetString(2),
                OrdemExibicao = r.GetInt32(3),
                Ativo = r.GetBoolean(4),
            });
        return lista;
    }

    public async Task<bool> CategoriaExisteAsync(int id, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand("SELECT EXISTS (SELECT 1 FROM categoria WHERE id_categoria = $1)");
        cmd.Parameters.AddWithValue(id);
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }

    /// <summary>Catálogo público: produtos ativos de categorias ativas.</summary>
    public async Task<List<Produto>> ListarCatalogoAsync(int? idCategoria, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand(SelectProduto +
            " WHERE p.ativo AND c.ativo AND ($1::int IS NULL OR p.id_categoria = $1) ORDER BY c.ordem_exibicao, p.nome");
        cmd.Parameters.Add(new NpgsqlParameter { Value = (object?)idCategoria ?? DBNull.Value, NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Integer });
        return await LerVariosAsync(cmd, ct);
    }

    public async Task<List<Produto>> ListarTodosAsync(CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand(SelectProduto + " ORDER BY c.ordem_exibicao, p.nome");
        return await LerVariosAsync(cmd, ct);
    }

    public async Task<Produto?> BuscarAsync(int id, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand(SelectProduto + " WHERE p.id_produto = $1");
        cmd.Parameters.AddWithValue(id);
        return (await LerVariosAsync(cmd, ct)).FirstOrDefault();
    }

    public async Task InserirAsync(Produto p, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand(
            "INSERT INTO produto (id_categoria, nome, descricao, preco, quantidade_estoque, imagem_url, ativo) " +
            "VALUES ($1, $2, $3, $4, $5, $6, $7) RETURNING id_produto");
        AdicionarParametros(cmd, p);
        p.IdProduto = (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task AtualizarAsync(Produto p, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand(
            "UPDATE produto SET id_categoria = $1, nome = $2, descricao = $3, preco = $4, " +
            "quantidade_estoque = $5, imagem_url = $6, ativo = $7 WHERE id_produto = $8");
        AdicionarParametros(cmd, p);
        cmd.Parameters.AddWithValue(p.IdProduto);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DefinirAtivoAsync(int id, bool ativo, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand("UPDATE produto SET ativo = $1 WHERE id_produto = $2");
        cmd.Parameters.AddWithValue(ativo);
        cmd.Parameters.AddWithValue(id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void AdicionarParametros(NpgsqlCommand cmd, Produto p)
    {
        cmd.Parameters.AddWithValue(p.IdCategoria);
        cmd.Parameters.AddWithValue(p.Nome);
        cmd.Parameters.AddWithValue((object?)p.Descricao ?? DBNull.Value);
        cmd.Parameters.AddWithValue(p.Preco);
        cmd.Parameters.AddWithValue(p.QuantidadeEstoque);
        cmd.Parameters.AddWithValue((object?)p.ImagemUrl ?? DBNull.Value);
        cmd.Parameters.AddWithValue(p.Ativo);
    }

    internal static Produto Ler(NpgsqlDataReader r) => new()
    {
        IdProduto = r.GetInt32(0),
        IdCategoria = r.GetInt32(1),
        NomeCategoria = r.GetString(2),
        Nome = r.GetString(3),
        Descricao = r.IsDBNull(4) ? null : r.GetString(4),
        Preco = r.GetDecimal(5),
        QuantidadeEstoque = r.GetInt32(6),
        ImagemUrl = r.IsDBNull(7) ? null : r.GetString(7),
        Ativo = r.GetBoolean(8),
        DataCadastro = r.GetDateTime(9),
    };

    private static async Task<List<Produto>> LerVariosAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var lista = new List<Produto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) lista.Add(Ler(r));
        return lista;
    }
}

public class PedidoRepositorio(NpgsqlDataSource db)
{
    private const string SelectPedido =
        "SELECT p.id_pedido, p.id_usuario, p.data_pedido, p.data_atualizacao, p.status, p.valor_total, " +
        "p.tipo_entrega, p.endereco_entrega, p.forma_pagamento, p.observacao, u.nome, u.email, u.telefone " +
        "FROM pedido p JOIN usuario u ON u.id_usuario = p.id_usuario";

    public NpgsqlDataSource Fonte => db;

    /// <summary>Lê os produtos e bloqueia as linhas (FOR UPDATE) até o fim da transação.</summary>
    public async Task<Dictionary<int, Produto>> BloquearProdutosAsync(
        NpgsqlConnection con, NpgsqlTransaction tx, int[] ids, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            ProdutoRepositorio.SelectProduto + " WHERE p.id_produto = ANY($1) FOR UPDATE OF p", con, tx);
        cmd.Parameters.AddWithValue(ids);
        var mapa = new Dictionary<int, Produto>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            var p = ProdutoRepositorio.Ler(r);
            mapa[p.IdProduto] = p;
        }
        return mapa;
    }

    public async Task InserirAsync(NpgsqlConnection con, NpgsqlTransaction tx, Pedido p, CancellationToken ct)
    {
        await using (var cmd = new NpgsqlCommand(
            "INSERT INTO pedido (id_usuario, status, valor_total, tipo_entrega, endereco_entrega, forma_pagamento, observacao) " +
            "VALUES ($1, $2, $3, $4, $5, $6, $7) RETURNING id_pedido", con, tx))
        {
            cmd.Parameters.AddWithValue(p.IdUsuario);
            cmd.Parameters.AddWithValue(p.Status.Codigo());
            cmd.Parameters.AddWithValue(p.ValorTotal);
            cmd.Parameters.AddWithValue(p.TipoEntrega.Codigo());
            cmd.Parameters.AddWithValue((object?)p.EnderecoEntrega ?? DBNull.Value);
            cmd.Parameters.AddWithValue(p.FormaPagamento.Codigo());
            cmd.Parameters.AddWithValue((object?)p.Observacao ?? DBNull.Value);
            p.IdPedido = (int)(await cmd.ExecuteScalarAsync(ct))!;
        }

        foreach (var item in p.Itens)
        {
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO item_pedido (id_pedido, id_produto, quantidade, preco_unitario) VALUES ($1, $2, $3, $4) RETURNING id_item",
                con, tx);
            cmd.Parameters.AddWithValue(p.IdPedido);
            cmd.Parameters.AddWithValue(item.IdProduto);
            cmd.Parameters.AddWithValue(item.Quantidade);
            cmd.Parameters.AddWithValue(item.PrecoUnitario);
            item.IdItem = (int)(await cmd.ExecuteScalarAsync(ct))!;
            item.IdPedido = p.IdPedido;
        }
    }

    public async Task AtualizarEstoqueAsync(NpgsqlConnection con, NpgsqlTransaction tx, Produto p, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("UPDATE produto SET quantidade_estoque = $1 WHERE id_produto = $2", con, tx);
        cmd.Parameters.AddWithValue(p.QuantidadeEstoque);
        cmd.Parameters.AddWithValue(p.IdProduto);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<Pedido?> BloquearPedidoAsync(NpgsqlConnection con, NpgsqlTransaction tx, int id, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(SelectPedido + " WHERE p.id_pedido = $1 FOR UPDATE OF p", con, tx);
        cmd.Parameters.AddWithValue(id);
        var lista = await LerPedidosAsync(cmd, ct);
        if (lista.Count == 0) return null;
        await CarregarItensAsync(con, tx, lista, ct);
        return lista[0];
    }

    public async Task AtualizarStatusAsync(NpgsqlConnection con, NpgsqlTransaction tx, Pedido p, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "UPDATE pedido SET status = $1, data_atualizacao = CURRENT_TIMESTAMP WHERE id_pedido = $2 RETURNING data_atualizacao",
            con, tx);
        cmd.Parameters.AddWithValue(p.Status.Codigo());
        cmd.Parameters.AddWithValue(p.IdPedido);
        p.DataAtualizacao = (DateTime)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task<Pedido?> BuscarAsync(int id, CancellationToken ct = default)
    {
        await using var con = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(SelectPedido + " WHERE p.id_pedido = $1", con);
        cmd.Parameters.AddWithValue(id);
        var lista = await LerPedidosAsync(cmd, ct);
        await CarregarItensAsync(con, null, lista, ct);
        return lista.FirstOrDefault();
    }

    public async Task<List<Pedido>> ListarDoClienteAsync(int idUsuario, CancellationToken ct = default)
    {
        await using var con = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(SelectPedido + " WHERE p.id_usuario = $1 ORDER BY p.id_pedido DESC", con);
        cmd.Parameters.AddWithValue(idUsuario);
        var lista = await LerPedidosAsync(cmd, ct);
        await CarregarItensAsync(con, null, lista, ct);
        return lista;
    }

    /// <summary>situacao: "abertos" (mais antigos primeiro), "concluidos" ou null (todos, mais recentes primeiro).</summary>
    public async Task<List<Pedido>> ListarParaAdminAsync(string? situacao, CancellationToken ct = default)
    {
        var filtro = situacao switch
        {
            "abertos" => " WHERE p.status NOT IN ('ENTREGUE', 'CANCELADO') ORDER BY p.id_pedido",
            "concluidos" => " WHERE p.status IN ('ENTREGUE', 'CANCELADO') ORDER BY p.id_pedido DESC",
            _ => " ORDER BY p.id_pedido DESC",
        };
        await using var con = await db.OpenConnectionAsync(ct);
        await using var cmd = new NpgsqlCommand(SelectPedido + filtro + " LIMIT 200", con);
        var lista = await LerPedidosAsync(cmd, ct);
        await CarregarItensAsync(con, null, lista, ct);
        return lista;
    }

    private static async Task<List<Pedido>> LerPedidosAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var lista = new List<Pedido>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new Pedido
            {
                IdPedido = r.GetInt32(0),
                IdUsuario = r.GetInt32(1),
                DataPedido = r.GetDateTime(2),
                DataAtualizacao = r.GetDateTime(3),
                Status = Codigos.ParseStatus(r.GetString(4)),
                ValorTotal = r.GetDecimal(5),
                TipoEntrega = Codigos.ParseTipoEntrega(r.GetString(6)),
                EnderecoEntrega = r.IsDBNull(7) ? null : r.GetString(7),
                FormaPagamento = Codigos.ParseFormaPagamento(r.GetString(8)),
                Observacao = r.IsDBNull(9) ? null : r.GetString(9),
                NomeCliente = r.GetString(10),
                EmailCliente = r.GetString(11),
                TelefoneCliente = r.IsDBNull(12) ? null : r.GetString(12),
            });
        return lista;
    }

    private static async Task CarregarItensAsync(NpgsqlConnection con, NpgsqlTransaction? tx, List<Pedido> pedidos, CancellationToken ct)
    {
        if (pedidos.Count == 0) return;
        var porId = pedidos.ToDictionary(p => p.IdPedido);
        await using var cmd = new NpgsqlCommand(
            "SELECT i.id_item, i.id_pedido, i.id_produto, pr.nome, pr.imagem_url, i.quantidade, i.preco_unitario " +
            "FROM item_pedido i JOIN produto pr ON pr.id_produto = i.id_produto " +
            "WHERE i.id_pedido = ANY($1) ORDER BY i.id_item", con, tx);
        cmd.Parameters.AddWithValue(porId.Keys.ToArray());
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            porId[r.GetInt32(1)].Itens.Add(new ItemPedido
            {
                IdItem = r.GetInt32(0),
                IdPedido = r.GetInt32(1),
                IdProduto = r.GetInt32(2),
                NomeProduto = r.GetString(3),
                ImagemUrl = r.IsDBNull(4) ? null : r.GetString(4),
                Quantidade = r.GetInt32(5),
                PrecoUnitario = r.GetDecimal(6),
            });
    }
}
