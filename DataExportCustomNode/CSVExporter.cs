using DataExportCustomNode.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DataExportCustomNode
{
    public class CSVExporter
    {
        private readonly string filePath;
        private bool fileCreated;
        private readonly List<string> exportColumns;
        private readonly List<ExportRow> exportRows;
        private readonly string delimiter;
        private readonly bool appendToExisting;
        private readonly bool includeColumnHeaders;
        private static readonly Mutex mutex = new Mutex(false, "S9CustomNodeCsvExporterMutex");

        public CSVExporter(string filePath, List<string> exportColumns, List<ExportRow> exportRows, string delimiter, bool appendToExisting, bool includeColumnHeaders)
        {
            this.fileCreated = true;
            this.filePath = VerifyFilePath(filePath, appendToExisting);
            this.exportColumns = exportColumns;
            this.exportRows = exportRows;
            this.delimiter = delimiter;
            this.appendToExisting = appendToExisting;
            this.includeColumnHeaders = includeColumnHeaders;
        }

        public void Export()
        {
            try
            {
                mutex.WaitOne();

                using (var fileStream = new FileStream(filePath, this.appendToExisting ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(fileStream))
                {
                    if (!this.appendToExisting || this.fileCreated)
                    {
                        string headers = string.Join(this.delimiter, this.exportColumns);
                        if (this.includeColumnHeaders)
                        {
                            writer.WriteLine(headers);
                        }
                    }

                    foreach (var exportRow in this.exportRows)
                    {
                        StringBuilder lineBuilder = new StringBuilder();
                        for (int i = 0; i < exportRow.Values.Count; i++)
                        {
                            string value = $"{exportRow.Values[i].Value}";
                            if (value.Contains(this.delimiter))
                            {
                                value = $"\"{value}\"";
                            }
                            string delimiter = (i != 0) ? (this.delimiter == string.Empty) ? "," : this.delimiter : string.Empty;
                            lineBuilder.Append(delimiter).Append(value);
                        }
                        string line = lineBuilder.ToString();
                        writer.WriteLine(line);
                    }

                    writer.Flush();
                    this.fileCreated = false;
                }
            }
            finally
            {
                mutex.ReleaseMutex();
            }
        }

        private string VerifyFilePath(string filePath, bool appendToExisting)
        {
            string directory = Path.GetDirectoryName(filePath);
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);
            int fileCount = 0;
            while (File.Exists(filePath))
            {
                this.fileCreated = false;
                if (!appendToExisting)
                {
                    fileCount++;
                    filePath = Path.Combine(directory, $"{fileNameWithoutExtension}_{fileCount}{extension}");
                }
                else
                {
                    break;
                }
            }
            return filePath;
        }
    }
}
