using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using Square9.CustomNode;
using System.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using System.Net.Http;
using DataExportCustomNode.Helpers;

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
            Result GSDocument = null;
            string UniqueID = Guid.NewGuid().ToString();

            try
            {
                //Get the GlobalSearch document
                using (HttpClient Client = Engine.GetSquare9ApiClient())
                {
                    RestRequests Connection = new RestRequests(Client);
                    string SecureId = Connection.GetDocumentSecureID(Process.Document.DatabaseId, Process.Document.ArchiveId, Process.Document.DocumentId);
                    string DocumentResult = Connection.GetDocument(Process.Document.DatabaseId, Process.Document.ArchiveId, Process.Document.DocumentId, SecureId);
                    GSDocument = JsonConvert.DeserializeObject<Result>(DocumentResult);
                }

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
                        CSVExporter csvExporter = new CSVExporter(ExportPath, ColumnName, Export, Delimiter, AppendToExisting, IncludeColumnHeaders);
                        csvExporter.Export();
                        LogHistory($"Data Exported to CSV: {ExportPath}");
                        break;
                    case "SQL":
                        SQLExporter sqlExporter = new SQLExporter(ExportPath, TableName, Export);
                        sqlExporter.Export();
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
                exportValue.Type = 1; //Set the SQL Column type to varchar after formatting is applied
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
    }
}
