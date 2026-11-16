using Cupcakes.Api.Models;
using Npgsql;

namespace Cupcakes.Api.Data;

/// <summary>Acesso à tabela feedback (database/03_feedback.sql).</summary>
public class FeedbackRepositorio(NpgsqlDataSource db)
{
    private const string Select =
        "SELECT f.id_feedback, f.id_usuario, f.id_pedido, f.id_produto, f.assunto, f.nota, f.comentario, " +
        "f.data_envio, f.lido, pr.nome, u.nome, u.email " +
        "FROM feedback f JOIN usuario u ON u.id_usuario = f.id_usuario " +
        "LEFT JOIN produto pr ON pr.id_produto = f.id_produto";

    public async Task InserirAsync(Feedback f, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand(
            "INSERT INTO feedback (id_usuario, id_pedido, id_produto, assunto, nota, comentario) " +
            "VALUES ($1, $2, $3, $4, $5, $6) RETURNING id_feedback, data_envio");
        cmd.Parameters.AddWithValue(f.IdUsuario);
        cmd.Parameters.AddWithValue((object?)f.IdPedido ?? DBNull.Value);
        cmd.Parameters.AddWithValue((object?)f.IdProduto ?? DBNull.Value);
        cmd.Parameters.AddWithValue(f.Assunto.Codigo());
        cmd.Parameters.AddWithValue((short)f.Nota);
        cmd.Parameters.AddWithValue(f.Comentario);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        f.IdFeedback = r.GetInt32(0);
        f.DataEnvio = r.GetDateTime(1);
    }

    public async Task<Feedback?> BuscarAsync(int id, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand(Select + " WHERE f.id_feedback = $1");
        cmd.Parameters.AddWithValue(id);
        return (await LerAsync(cmd, ct)).FirstOrDefault();
    }

    public async Task<List<Feedback>> ListarDoClienteAsync(int idUsuario, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand(Select + " WHERE f.id_usuario = $1 ORDER BY f.id_feedback DESC");
        cmd.Parameters.AddWithValue(idUsuario);
        return await LerAsync(cmd, ct);
    }

    /// <summary>situacao: "nao_lidos" ou null (todos). Mais recentes primeiro.</summary>
    public async Task<List<Feedback>> ListarParaAdminAsync(string? situacao, CancellationToken ct = default)
    {
        var filtro = situacao == "nao_lidos" ? " WHERE NOT f.lido" : "";
        await using var cmd = db.CreateCommand(Select + filtro + " ORDER BY f.id_feedback DESC LIMIT 300");
        return await LerAsync(cmd, ct);
    }

    /// <summary>Totais para o painel: quantidade, não lidos e média das notas por assunto.</summary>
    public async Task<(int Total, int NaoLidos, decimal? MediaProduto, decimal? MediaAtendimento)> ResumoAsync(CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand(
            "SELECT COUNT(*)::int, COUNT(*) FILTER (WHERE NOT lido)::int, " +
            "ROUND(AVG(nota) FILTER (WHERE assunto = 'PRODUTO'), 1), " +
            "ROUND(AVG(nota) FILTER (WHERE assunto = 'ATENDIMENTO'), 1) FROM feedback");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return (r.GetInt32(0), r.GetInt32(1),
            r.IsDBNull(2) ? null : r.GetDecimal(2),
            r.IsDBNull(3) ? null : r.GetDecimal(3));
    }

    public async Task<bool> DefinirLidoAsync(int id, bool lido, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand("UPDATE feedback SET lido = $1 WHERE id_feedback = $2");
        cmd.Parameters.AddWithValue(lido);
        cmd.Parameters.AddWithValue(id);
        return await cmd.ExecuteNonQueryAsync(ct) > 0;
    }

    private static async Task<List<Feedback>> LerAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var lista = new List<Feedback>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            lista.Add(new Feedback
            {
                IdFeedback = r.GetInt32(0),
                IdUsuario = r.GetInt32(1),
                IdPedido = r.IsDBNull(2) ? null : r.GetInt32(2),
                IdProduto = r.IsDBNull(3) ? null : r.GetInt32(3),
                Assunto = Codigos.ParseAssunto(r.GetString(4)),
                Nota = r.GetInt16(5),
                Comentario = r.GetString(6),
                DataEnvio = r.GetDateTime(7),
                Lido = r.GetBoolean(8),
                NomeProduto = r.IsDBNull(9) ? null : r.GetString(9),
                NomeCliente = r.GetString(10),
                EmailCliente = r.GetString(11),
            });
        return lista;
    }
}
