using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateReportSystem
{
    public struct PageInformation
    {
        public double StartFootage { get; set; }
        public double EndFootage { get; set; }
        public double Width => EndFootage - StartFootage;
        public int PageNumber { get; set; }
        public int TotalPages { get; set; }
    }
}
