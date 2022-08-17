using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PatchClientWebservices
{
    public class WSClass
    {
        public string Name { get; set; }
        public string EntityFile { get; set; }
        public bool IsChecked { get; set; }
        public bool Force { get; set; }

        public WSClass() { IsChecked = false; Force = false; }
    }
}
