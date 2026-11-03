using Npgsql;

namespace Cupcakes.Api.Data;

/// <summary>
/// Cria as tabelas e a carga inicial na primeira execução, usando os mesmos scripts
/// de database/ que fazem parte da documentação. Se as tabelas já existem, não faz nada.
/// </summary>
public static class InicializadorBanco
{
    public static async Task<bool> GarantirCriadoAsync(NpgsqlDataSource db, ILogger logger, CancellationToken ct = default)
    {
        await using (var cmd = db.CreateCommand("SELECT to_regclass('public.usuario') IS NOT NULL"))
        {
            if ((bool)(await cmd.ExecuteScalarAsync(ct))!)
                return false;
        }

        var pasta = Path.Combine(AppContext.BaseDirectory, "database");
        foreach (var arquivo in new[] { "01_schema.sql", "02_seed.sql" })
        {
            logger.LogInformation("Executando {Arquivo}", arquivo);
            var sql = await File.ReadAllTextAsync(Path.Combine(pasta, arquivo), ct);
            await using var cmd = db.CreateCommand(sql);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        logger.LogInformation("Banco de dados criado com a carga inicial.");
        return true;
    }
}
