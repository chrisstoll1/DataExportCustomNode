using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataExportCustomNode.Helpers
{
    public class FieldItem
    {
        public FieldItem() { }
        public int Id { get; set; }
        public List<string> Mval { get; set; }
        public string Val { get; set; }
    }
}
