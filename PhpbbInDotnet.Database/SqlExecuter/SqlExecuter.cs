﻿using Dapper;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Domain;
using Serilog;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Database.SqlExecuter
{
    class SqlExecuter : DapperProxy, ISqlExecuter
    {
		public SqlExecuter(IConfiguration configuration, IDbConnection dbConnection, ILogger logger) : base(configuration, dbConnection, logger) { }

		public IEnumerable<T> CallStoredProcedure<T>(string storedProcedureName, object? param, IDbTransaction? dbTransaction)
            => ResilientExecute(() => Connection.Query<T>(BuildStoreProcedureCall(storedProcedureName, param), param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        public Task<IEnumerable<T>> CallStoredProcedureAsync<T>(string storedProcedureName, object? param, IDbTransaction? dbTransaction)
            => ResilientExecuteAsync(() => Connection.QueryAsync<T>(BuildStoreProcedureCall(storedProcedureName, param), param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        public Task<SqlMapper.GridReader> CallMultipleResultsStoredProcedureAsync(string storedProcedureName, object? param, IDbTransaction? dbTransaction)
            => QueryMultipleAsync(BuildStoreProcedureCall(storedProcedureName, param), param, dbTransaction);

        public Task CallStoredProcedureAsync(string storedProcedureName, object? param, IDbTransaction? dbTransaction)
            => ResilientExecuteAsync(() => Connection.QueryAsync(BuildStoreProcedureCall(storedProcedureName, param), param, transaction: dbTransaction, commandTimeout: TIMEOUT));

        public IDapperProxy WithPagination(int skip, int take)
			=> new PaginatedDapperProxy(this, DatabaseType, skip, take);

        public string LastInsertedItemId => DatabaseType switch
        {
            DatabaseType.MySql => "LAST_INSERT_ID()",
            DatabaseType.SqlServer => "SCOPE_IDENTITY()",
            _ => throw new ArgumentException("Unknown Database type in configuration.")
        };

        public string PaginationWildcard => PaginatedDapperProxy.PAGINATION_WILDCARD;

        private string BuildStoreProcedureCall(string storedProcedureName, object? param)
        {
            var format = DatabaseType switch
            {
                DatabaseType.MySql => "CALL {0}({1})",
                DatabaseType.SqlServer => "EXEC {0} {1}",
                _ => throw new ArgumentException("Unknown Database type in configuration.")
            };
            var @params = param is not null ? param.GetType().GetProperties().Select(prop => $"@{prop.Name}") : Enumerable.Empty<string>();
            return string.Format(format, storedProcedureName, string.Join(",", @params));
        }
	}
}
