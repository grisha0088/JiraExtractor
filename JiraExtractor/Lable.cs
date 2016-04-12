using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JiraExtractor
{
    public class Lable
    {
        public int id { get; set; }
        public string value { get; set; }
        public List<Ticket> Ticket { get; set; }
    }
}
