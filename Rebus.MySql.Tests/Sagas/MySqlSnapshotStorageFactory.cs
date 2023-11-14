﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Rebus.Auditing.Sagas;
using Rebus.Logging;
using Rebus.MySql.Sagas;
using Rebus.Sagas;
using Rebus.Serialization;
using Rebus.Tests.Contracts.Sagas;

namespace Rebus.MySql.Tests.Sagas;

public class MySqlSnapshotStorageFactory : ISagaSnapshotStorageFactory
{
    const string TableName = "SagaSnapshots";

    public ISagaSnapshotStorage Create()
    {
        var consoleLoggerFactory = new ConsoleLoggerFactory(true);
        var connectionProvider = new DbConnectionProvider(MySqlTestHelper.ConnectionString, consoleLoggerFactory);

        var snapperino = new MySqlSagaSnapshotStorage(connectionProvider, TableName, consoleLoggerFactory);

        snapperino.EnsureTableIsCreated();

        return snapperino;
    }

    public IEnumerable<SagaDataSnapshot> GetAllSnapshots()
    {
        return LoadStoredCopies(new DbConnectionProvider(MySqlTestHelper.ConnectionString, new ConsoleLoggerFactory(true)), TableName).Result;
    }

    static async Task<List<SagaDataSnapshot>> LoadStoredCopies(DbConnectionProvider connectionProvider, string tableName)
    {
        var storedCopies = new List<SagaDataSnapshot>();

        using var connection = await connectionProvider.GetConnectionAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $@"SELECT * FROM {tableName}";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var sagaData = (ISagaData)new ObjectSerializer().DeserializeFromString((string)reader["data"]);
                var metadata = new HeaderSerializer().DeserializeFromString((string)reader["metadata"]);
                storedCopies.Add(new SagaDataSnapshot { SagaData = sagaData, Metadata = metadata });
            }
        }
        await connection.CompleteAsync();
        return storedCopies;
    }

    public void Dispose() => MySqlTestHelper.DropTable(TableName);
}
