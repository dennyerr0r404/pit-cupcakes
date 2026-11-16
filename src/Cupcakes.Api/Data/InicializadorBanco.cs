using Npgsql;

namespace Cupcakes.Api.Data;

/// <summary>
/// Prepara o banco usando os mesmos scripts de database/ que fazem parte da documentação:
/// na primeira execução cria as tabelas e a carga inicial (01 e 02); em toda execução
/// aplica os scripts complementares, que podem ser repetidos com segurança (03 em diante).
/// </summary>
public static class InicializadorBanco
{
    private static readonly string[] ScriptsIniciais = ["01_schema.sql", "02_seed.sql"];
    private static readonly string[] ScriptsComplementares = ["03_feedback.sql", "04_melhorias_laudo.sql"];

    public static async Task<bool> GarantirCriadoAsync(NpgsqlDataSource db, ILogger logger, CancellationToken ct = default)
    {
        bool existe;
        await using (var cmd = db.CreateCommand("SELECT to_regclass('public.usuario') IS NOT NULL"))
            existe = (bool)(await cmd.ExecuteScalarAsync(ct))!;

        if (!existe)
            foreach (var arquivo in ScriptsIniciais)
                await ExecutarAsync(db, arquivo, logger, ct);

        foreach (var arquivo in ScriptsComplementares)
            await ExecutarAsync(db, arquivo, logger, ct);

        if (!existe)
            logger.LogInformation("Banco de dados criado com a carga inicial.");
        return !existe;
    }

    private static async Task ExecutarAsync(NpgsqlDataSource db, string arquivo, ILogger logger, CancellationToken ct)
    {
        logger.LogInformation("Executando {Arquivo}", arquivo);
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "database", arquivo), ct);
        await using var cmd = db.CreateCommand(sql);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
