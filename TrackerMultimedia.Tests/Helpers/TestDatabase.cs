using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;
using TrackerMultimedia.Data;

namespace TrackerMultimedia.Tests.Helpers;

/// <summary>
/// Bases de datos PostgreSQL desechables para la suite.
///
/// **Por qué no SQLite (T2-02).** La suite corría sobre SQLite en memoria, así que nada
/// específico del proveedor real se comprobaba. No era teórico: `GET /api/media-items`
/// con el parámetro `search` devuelve **500** en SQLite, porque `EF.Functions.ILike` solo
/// existe en Npgsql. Una funcionalidad visible del producto no podía tener ni un test
/// mientras la suite no hablara con PostgreSQL.
///
/// **Cómo.** Una vez por ejecución se crea una base plantilla y se le aplican las
/// **migraciones** —no `EnsureCreated`, que construye el esquema desde el modelo y salta
/// las migraciones por completo; eso es lo que hizo que nadie detectara que `Migrate()`
/// estaba comentado (T2-03)—. Cada clase de test copia esa plantilla con
/// `CREATE DATABASE ... TEMPLATE`, que es una operación de archivos y tarda milisegundos,
/// en vez de repetir las migraciones veinte veces.
///
/// De paso desaparece la `SqliteConnection` compartida entre todos los `DbContext`, que
/// era la causa de la carrera intermitente de T2-28.
/// </summary>
internal static class TestDatabase
{
    private const string Prefix = "tm_test_";

    private static readonly Lazy<string> AdminConnectionString = new(ResolveAdminConnectionString);
    private static readonly Lazy<string> Template = new(CreateTemplate);
    private static readonly ConcurrentDictionary<string, byte> Created = new();

    /// <summary>Crea una base para una clase de test y devuelve su cadena de conexión.</summary>
    public static string CreateForTestClass()
    {
        var templateName = Template.Value;
        var name = $"{Prefix}{Guid.NewGuid():N}";

        // TEMPLATE exige que nadie esté conectado a la plantilla. Nadie lo está: se creó,
        // se migró y se cerró la conexión antes de llegar aquí.
        Execute(AdminConnectionString.Value, $"""CREATE DATABASE "{name}" TEMPLATE "{templateName}";""");
        Created.TryAdd(name, 0);

        return BuildConnectionString(name);
    }

    public static void Drop(string connectionString)
    {
        var name = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        DropDatabase(name);
        Created.TryRemove(name, out _);
    }

    // ---------------------------------------------------------------------------

    private static string ResolveAdminConnectionString()
    {
        // La cadena sale de una variable de entorno o, si no está, de los mismos
        // user-secrets que usa la aplicación: así no hay una credencial más que rotar
        // ni un archivo de configuración de tests con una contraseña dentro.
        var configured = Environment.GetEnvironmentVariable("TRACKERMULTIMEDIA_TEST_POSTGRES");

        if (string.IsNullOrWhiteSpace(configured))
        {
            var secrets = new ConfigurationBuilder()
                .AddUserSecrets("f8b4c210-3a9e-4d52-b71f-6c8a30d5e294")
                .Build();
            configured = secrets.GetConnectionString("DefaultConnection");
        }

        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                "La suite necesita PostgreSQL. Define TRACKERMULTIMEDIA_TEST_POSTGRES con una cadena " +
                "de conexión, o configura ConnectionStrings:DefaultConnection en los user-secrets del " +
                "backend (`dotnet user-secrets set`). Nunca se toca la base de la aplicación: de esa " +
                "cadena solo se reutilizan servidor y credenciales, y cada ejecución crea las suyas.");
        }

        // Conectarse a `postgres` para poder crear y borrar bases: no se toca la base de
        // la aplicación, solo se aprovechan el servidor y las credenciales.
        return new NpgsqlConnectionStringBuilder(configured) { Database = "postgres" }.ConnectionString;
    }

    private static string BuildConnectionString(string database)
        => new NpgsqlConnectionStringBuilder(AdminConnectionString.Value)
        {
            Database = database,
            // Sin pool: cada base vive lo que dura una clase de test, y una conexión
            // agrupada que sobreviva impide el DROP DATABASE del final.
            Pooling = false,
        }.ConnectionString;

    private static string CreateTemplate()
    {
        DropStaleDatabases();

        var name = $"{Prefix}template_{Guid.NewGuid():N}";
        Execute(AdminConnectionString.Value, $"""CREATE DATABASE "{name}";""");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(BuildConnectionString(name))
            .Options;

        using (var context = new ApplicationDbContext(options))
        {
            // Migrate y no EnsureCreated: así una migración rota hace fallar la suite,
            // que es justo lo que no ocurría antes (T2-03).
            context.Database.Migrate();
        }

        NpgsqlConnection.ClearAllPools();
        return name;
    }

    /// <summary>
    /// Restos de ejecuciones anteriores interrumpidas. Se limpian al empezar en vez de
    /// confiar en un cierre ordenado, que es justo lo que no ocurre cuando se cancela
    /// una ejecución a medias.
    /// </summary>
    private static void DropStaleDatabases()
    {
        using var connection = new NpgsqlConnection(AdminConnectionString.Value);
        connection.Open();

        var names = new List<string>();
        using (var command = new NpgsqlCommand(
            "SELECT datname FROM pg_database WHERE datname LIKE @prefix || '%';", connection))
        {
            command.Parameters.AddWithValue("prefix", Prefix);
            using var reader = command.ExecuteReader();
            while (reader.Read())
                names.Add(reader.GetString(0));
        }

        foreach (var name in names)
            DropDatabase(name);
    }

    private static void DropDatabase(string name)
    {
        try
        {
            NpgsqlConnection.ClearAllPools();
            Execute(AdminConnectionString.Value,
                $"""DROP DATABASE IF EXISTS "{name}" WITH (FORCE);""");
        }
        catch (PostgresException)
        {
            // Que no se pueda borrar una base de test no debe tumbar la suite: la
            // siguiente ejecución la recogerá en DropStaleDatabases.
        }
    }

    private static void Execute(string connectionString, string sql)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var command = new NpgsqlCommand(sql, connection);
        command.ExecuteNonQuery();
    }
}
