using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using Square9.CustomNode;
using Square9APIHelperLibrary;
using Square9APIHelperLibrary.DataTypes;
using System.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace DataExportCustomNode
{
    public class DataExport : ActionNode
    {
        public override void Run()
        {
            List<string> propertyNames = Process.Properties.GetPropertyNames();
            List<string> Fields = Settings.GetListSetting("Fields");
            List<string> ColumnName = Settings.GetListSetting("ColumnName");
            List<string> FieldOptions = Settings.GetListSetting("Options");
            string ExportType = Settings.GetStringSetting("ExportType");
            string ExportPath = Settings.GetStringSetting("ExportPath");
            string TableName = Settings.GetStringSetting("TableName");
            string Delimiter = Settings.GetStringSetting("Delimiter");
            bool IncludeColumnHeaders = Settings.GetBooleanSetting("IncludeColumnHeaders");
            bool AppendToExisting = Settings.GetBooleanSetting("AppendToExisting");
            List<ExportRow> Export = new List<ExportRow>();
            Square9API Connection;
            string UniqueID = Guid.NewGuid().ToString();

            try
            {
                //Extract Square9API info from built in client
                using (var square9Api = Engine.GetSquare9ApiClient())
                {

                    string[] Creds = new string(Convert.FromBase64String(square9Api.DefaultRequestHeaders.Authorization.Parameter).Select(b => (char)b).ToArray()).Split(':');
                    string Username = Creds[0];
                    string Password = Creds[1];
                    string Endpoint = square9Api.BaseAddress.ToString().Replace("api/", "");
                    Connection = new Square9API(Endpoint, Username, Password);
                }

                //Get Document
                Connection.CreateLicense();
                Result GSDocument = Connection.GetArchiveDocument(Process.Document.DatabaseId, Process.Document.ArchiveId, Process.Document.DocumentId);
                Connection.DeleteLicense();

                //Build the export based on specified fields
                foreach (var Document in GSDocument.Docs) 
                {
                    ExportRow exportRow = new ExportRow();
                    exportRow.Values = new List<ExportValue>();
                    for (int i = 0; i < Fields.Count; i++)
                    {
                        ExportValue exportValue = new ExportValue();
                        exportValue.ColumnName = ColumnName[i];
                        switch (Fields[i])
                        {
                            case "DocID":
                                exportValue.Value = Process.Document.DocumentId.ToString();
                                exportValue.Type = 1;
                                break;
                            case "ArchiveID":
                                exportValue.Value = Process.Document.ArchiveId.ToString();
                                exportValue.Type = 1;
                                break;
                            case "IID":
                                exportValue.Value = UniqueID;
                                exportValue.Type = 1;
                                break;
                            default:
                                try //Try to use a property 
                                {
                                    exportValue.Value = Process.Properties.GetPropertyByName(Fields[i]).Value;
                                    exportValue.Type = Process.Properties.GetPropertyByName(Fields[i]).Type;
                                }
                                catch (Exception) //If no property exists, use the document field
                                {
                                    bool fieldDoesNotExistFlag = true;
                                    foreach (var field in GSDocument.Fields)
                                    {
                                        if (field.Name == Fields[i])
                                        {
                                            foreach (var docField in Document.Fields)
                                            {
                                                if (docField.Id == field.Id)
                                                {
                                                    exportValue.Value = docField.Mval.Count() > 0 ? docField.Mval[0] : docField.Val;
                                                    exportValue.Type = field.Type;
                                                    fieldDoesNotExistFlag = false;
                                                    break;
                                                }
                                            }
                                            break;
                                        }
                                    }
                                    if (fieldDoesNotExistFlag)
                                    {
                                        throw new Exception($"Document Field Does Not Exist: {Fields[i]}");
                                    }
                                }

                                if (ValidateJSON(FieldOptions[i])) //Apply formatting if it exists
                                {
                                    FieldOptions Options = JsonConvert.DeserializeObject<FieldOptions>(FieldOptions[i]);
                                    try
                                    {
                                        exportValue = ApplyFormatting(exportValue, Options);
                                    }
                                    catch (Exception ex)
                                    {
                                        throw new Exception($"Could Not Apply Formatting: {Fields[i]} | {ex.Message}");
                                    }
                                }
                                break;
                        }

                        exportRow.Values.Add(exportValue);
                    }
                    Export.Add(exportRow);
                }

                //Export
                switch (ExportType)
                {
                    case "CSV":
                        bool fileCreated = false;
                        foreach (var exportRow in Export)
                        {
                            string line = "";
                            string headers = "";
                            for (int i = 0; i < exportRow.Values.Count; i++) //Generate Header/Line
                            {
                                string value = $"{exportRow.Values[i].Value}";
                                if (value.Contains(Delimiter))
                                {
                                    value = $"\"{value}\"";
                                }
                                string delimiter = (i != 0) ? (Delimiter == string.Empty) ? "," : Delimiter : string.Empty;
                                line = $"{line}{delimiter}{value}";
                                headers = $"{headers}{delimiter}{ColumnName[i]}";
                            }

                            bool exists = System.IO.File.Exists(ExportPath);
                            if (exists && !AppendToExisting && !fileCreated)
                            {
                                int lastIndex = ExportPath.LastIndexOf('.');
                                var name = ExportPath.Substring(0, lastIndex);
                                var ext = ExportPath.Substring(lastIndex + 1);
                                int fileCount = 0;
                                do
                                {
                                    fileCount++;
                                } 
                                while (System.IO.File.Exists($"{name}_{fileCount}.{ext}"));
                                ExportPath = $"{name}_{fileCount}.{ext}";
                                exists = false;
                            }

                            fileCreated = true;

                            using (StreamWriter sw = System.IO.File.AppendText(ExportPath)) //Write to File
                            {
                                if (!exists && IncludeColumnHeaders)
                                {
                                    sw.WriteLine(headers);
                                }
                                sw.WriteLine(line);
                            }
                        }

                        LogHistory($"Data Exported to CSV: {ExportPath}");
                        break;
                    case "SQL":
                        using (SqlConnection connection = new SqlConnection(ExportPath))
                        {
                            connection.Open();

                            bool tableExists;
                            bool tableCreated = false;
                            SqlCommand checkIfTableExists = new SqlCommand($"SELECT CASE WHEN EXISTS((SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = '{TableName}')) THEN 1 ELSE 0 END", connection);
                            tableExists = (int)checkIfTableExists.ExecuteScalar() == 1;

                            List<string> existingColumns = new List<string>();
                            if (tableExists)
                            {
                                SqlCommand getTableColumns = new SqlCommand($"SELECT C.NAME FROM sys.columns C INNER JOIN sys.tables T on T.object_id = C.object_id AND T.NAME = '{TableName}' AND T.TYPE = 'U'", connection);
                                using (var reader = getTableColumns.ExecuteReader())
                                {
                                    while (reader.Read())
                                    {
                                        existingColumns.Add(reader.GetString(0));
                                    }
                                }
                            }

                            foreach (var exportRow in Export)
                            {

                                string newColumns = "";
                                string columns = "";
                                string values = "";
                                for (int i = 0; i < exportRow.Values.Count; i++) //Generate Columns/Values
                                {
                                    //LogHistory($"{exportRow.Values[i].Value}");
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
                                    values = $"{values}{delimiter}'{value}'";

                                    if (tableExists && !existingColumns.Contains(exportRow.Values[i].ColumnName) && !tableCreated) //create new column if it does not exist
                                    {
                                        SqlCommand addTableColumn = new SqlCommand($"ALTER TABLE {TableName} ADD [{exportRow.Values[i].ColumnName}] {datatype}", connection);
                                        addTableColumn.ExecuteNonQuery();
                                    }
                                }

                                if (!tableExists && !tableCreated)
                                {
                                    SqlCommand createTableCommand = new SqlCommand($"CREATE TABLE [{TableName}] ({newColumns})", connection);
                                    createTableCommand.ExecuteNonQuery();
                                    tableCreated = true;
                                }

                                SqlCommand updateTableCommand = new SqlCommand($"INSERT INTO [{TableName}] ({columns}) VALUES ({values})", connection);
                                updateTableCommand.ExecuteNonQuery();
                            }
                        }
                        LogHistory($"Data Exported to SQL: {TableName}");
                        break;
                }

                SetNextNodeByLinkName("Exported");
            }
            catch (Exception ex)
            {
                LogHistory($"{ex.Message}");
                SetNextNodeByLinkName("Error");
            }
        }
        internal ExportValue ApplyFormatting(ExportValue exportValue, FieldOptions fieldOptions)
        {
            ExportValue formattedValue = exportValue;
            foreach (Replacement replacement in fieldOptions.Replacements)
            {
                if (replacement.P != "")
                {
                    formattedValue.Value = Regex.Replace(formattedValue.Value, replacement.P, replacement.R);
                }
            }
            if (fieldOptions.Format != "")
            {
                switch (exportValue.Type)
                {
                    case 2: //int
                        formattedValue.Value = Int32.Parse(exportValue.Value).ToString(fieldOptions.Format);
                        break;
                    case 3: //date
                        formattedValue.Value = DateTime.Parse(exportValue.Value).ToString(fieldOptions.Format);
                        break;
                    case 4: //float
                        formattedValue.Value = float.Parse(exportValue.Value).ToString(fieldOptions.Format);
                        break;
                }
                exportValue.Type = 1; //Set the type to String after formatting is applied
            }
            return formattedValue;
        }
        internal bool ValidateJSON(string json)
        {
            try
            {
                JsonConvert.DeserializeObject(json);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        internal class ExportRow
        {
            public List<ExportValue> Values { get; set; }
        }
        internal class ExportValue
        {
            public string ColumnName { get; set; }
            public int Type { get; set; }
            public string Value { get; set; }
        }
        internal class FieldOptions
        {
            public string Format { get; set; }
            public List<Replacement> Replacements { get; set; }
        }
        internal class Replacement
        {
            public string P { get; set; }
            public string R { get; set; }
        }
    }

}
