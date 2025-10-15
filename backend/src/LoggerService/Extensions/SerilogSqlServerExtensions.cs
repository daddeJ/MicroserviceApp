using System.Collections.ObjectModel;
using System.Data;
using Microsoft.Data.SqlClient;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;

namespace LoggerService.Extensions;

public static class SerilogSqlLoggerExtensions
{
    public static LoggerConfiguration AddCustomSqlLogger(
        this LoggerConfiguration configuration,
        string connectionString)
    {
        var columnOptions = new ColumnOptions();
        columnOptions.Store.Remove(StandardColumn.Properties);
        columnOptions.Store.Add(StandardColumn.LogEvent);

        // Add custom ApplicationLogId column
        columnOptions.AdditionalColumns = new Collection<SqlColumn>
        {
            new SqlColumn
            {
                ColumnName = "ApplicationLogId",
                DataType = SqlDbType.NVarChar,
                DataLength = 100,
                AllowNull = false
            }
        };

        // Configure Serilog MSSqlServer sink
        configuration.WriteTo.MSSqlServer(
            connectionString: connectionString,
            sinkOptions: new MSSqlServerSinkOptions
            {
                TableName = "ApplicationLogs",
                SchemaName = "dbo",
                AutoCreateSqlTable = true,
                BatchPostingLimit = 50,
                BatchPeriod = TimeSpan.FromSeconds(5)
            },
            columnOptions: columnOptions,
            restrictedToMinimumLevel: LogEventLevel.Information
        );

        // Add UNIQUE constraint safely after table creation
        EnsureUniqueConstraint(connectionString);

        return configuration;
    }

    private static void EnsureUniqueConstraint(string connectionString)
    {
        try
        {
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            var sql = @"
                IF NOT EXISTS (
                    SELECT * FROM sys.indexes 
                    WHERE name = 'UQ_ApplicationLogs_ApplicationLogId'
                      AND object_id = OBJECT_ID('dbo.ApplicationLogs')
                )
                BEGIN
                    ALTER TABLE dbo.ApplicationLogs
                    ADD CONSTRAINT UQ_ApplicationLogs_ApplicationLogId UNIQUE (ApplicationLogId);
                END";

            using var command = new SqlCommand(sql, connection);
            command.ExecuteNonQuery();

            Console.WriteLine("✅ UNIQUE constraint verified/added for ApplicationLogId");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not ensure UNIQUE constraint: {ex.Message}");
        }
    }
}
