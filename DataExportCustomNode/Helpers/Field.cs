using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataExportCustomNode.Helpers
{
    public class Field
    {
        public Field() { }
        public int Id { get; set; }
        public int List { get; set; }
        public int ListF1 { get; set; }
        public int ListF2 { get; set; }
        public string Mask { get; set; }
        public string Name { get; set; }
        public int Parent { get; set; }
        public int Prop { get; set; }
        public string RegEx { get; set; }
        public int Size { get; set; }
        public int Type { get; set; }
    }
}
