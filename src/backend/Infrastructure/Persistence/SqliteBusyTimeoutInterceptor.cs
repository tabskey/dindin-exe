using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence;

// SQLite por padrão falha imediatamente (SQLITE_BUSY) quando duas conexões escrevem ao
// mesmo tempo; o timeout faz a segunda esperar o lock e seguir o fluxo normal de retry
// do RowVersion, em vez de estourar erro de banco.
public sealed class SqliteBusyTimeoutInterceptor : DbConnectionInterceptor
{
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetBusyTimeout(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override Task ConnectionOpenedAsync(
        DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        SetBusyTimeout(connection);
        return base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private static void SetBusyTimeout(DbConnection connection)
    {
        if (connection is not SqliteConnection sqlite)
        {
            return;
        }

        using var command = sqlite.CreateCommand();
        command.CommandText = "PRAGMA busy_timeout = 30000;";
        command.ExecuteNonQuery();
    }
}
