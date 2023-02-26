using DataExportCustomNode.Helpers;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataExportCustomNode
{
    public class SQLExporter
    {
        private readonly string connectionString;
        private readonly string tableName;
        private readonly List<ExportRow> exportRows;
        private static readonly Mutex mutex = new Mutex(false, "S9CustomNodeSqlExporterMutex");

        public SQLExporter(string connectionString, string tableName, List<ExportRow> exportRows)
        {
            this.connectionString = connectionString;
            this.tableName = tableName;
            this.exportRows = exportRows;
        }

        public void Export()
        {
            try
            {
                mutex.WaitOne();

                using (SqlConnection connection = new SqlConnection(this.connectionString))
                {
                    connection.Open();

                    using (SqlTransaction transaction = connection.BeginTransaction())
                    {
                        try
                        {
                            bool tableExists;
                            bool tableCreated = false;
                            SqlCommand checkIfTableExists = new SqlCommand($"SELECT CASE WHEN EXISTS((SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = @tableName)) THEN 1 ELSE 0 END", connection, transaction);
                            checkIfTableExists.Parameters.AddWithValue("@tableName", this.tableName);
                            tableExists = (int)checkIfTableExists.ExecuteScalar() == 1;

                            List<string> existingColumns = new List<string>();
                            if (tableExists)
                            {
                                SqlCommand getTableColumns = new SqlCommand($"SELECT C.NAME FROM sys.columns C INNER JOIN sys.tables T on T.object_id = C.object_id AND T.NAME = @tableName AND T.TYPE = 'U'", connection, transaction);
                                getTableColumns.Parameters.AddWithValue("@tableName", this.tableName);
                                using (var reader = getTableColumns.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        existingColumns.Add(reader.GetString(0));
                                    }
                                }
                            }

                            foreach (var exportRow in this.exportRows)
                            {
                                string newColumns = "";
                                string columns = "";
                                string values = "";
                                List<SqlParameter> parameters = new List<SqlParameter>();
                                for (int i = 0; i < exportRow.Values.Count; i++) //Generate Columns/Values
                                {
                                    int type = exportRow.Values[i].Type;
                                    string value = exportRow.Values[i].Value;

                                    string datatype = "";
                                    switch (type)
                                    {
                                        case 1:
                                            datatype = "VARCHAR(MAX)";
                                            break;
                                        case 2:
                                            datatype = "INT";
                                            break;
                                        case 3:
                                            datatype = "DATE";
                                            break;
                                        case 4:
                                            datatype = "FLOAT";
                                            break;
                                    }
                                    string delimiter = (i != 0) ? "," : string.Empty;
                                    newColumns = $"{newColumns}{delimiter}[{exportRow.Values[i].ColumnName}] {datatype}";
                                    columns = $"{columns}{delimiter}[{exportRow.Values[i].ColumnName}]";
                                    values = $"{values}{delimiter}@{i}";
                                    parameters.Add(new SqlParameter($"@{i}", value));

                                    if (tableExists && !existingColumns.Contains(exportRow.Values[i].ColumnName) && !tableCreated) //create new column if it does not exist
                                    {
                                        SqlCommand addTableColumn = new SqlCommand($"ALTER TABLE {this.tableName} ADD [{exportRow.Values[i].ColumnName}] {datatype}", connection, transaction);
                                        addTableColumn.ExecuteNonQuery();
                                        existingColumns.Add(exportRow.Values[i].ColumnName);
                                    }
                                }

                                if (!tableExists && !tableCreated)
                                {
                                    SqlCommand createTableCommand = new SqlCommand($"CREATE TABLE [{this.tableName}] ({newColumns})", connection, transaction);
                                    createTableCommand.ExecuteNonQuery();
                                    tableCreated = true;
                                }

                                SqlCommand updateTableCommand = new SqlCommand($"INSERT INTO [{this.tableName}] ({columns}) VALUES ({values})", connection, transaction);
                                updateTableCommand.Parameters.AddRange(parameters.ToArray());
                                updateTableCommand.ExecuteNonQuery();
                            }

                            transaction.Commit();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback();
                            throw ex;
                        }
                    }
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }
    }
}
