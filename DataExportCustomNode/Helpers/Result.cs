using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataExportCustomNode.Helpers
{
    public class Result
    {
        public Result() { }
        public int Count { get; set; }
        public List<Doc> Docs { get; set; }
        public List<Field> Fields { get; set; }
    }
}
