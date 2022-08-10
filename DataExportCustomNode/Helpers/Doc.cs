using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataExportCustomNode.Helpers
{
    public class Doc
    {
        public Doc() { }
        public List<FieldItem> Fields { get; set; }
        public string FileType { get; set; }
        public string Hash { get; set; }
        public int Id { get; set; }
    }
}
