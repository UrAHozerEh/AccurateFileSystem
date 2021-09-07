using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateReportSystem
{
    public class PageSetup
    {
        public static PageSetup HourPageSetup = new PageSetup(12, 0);
        public double UnitsPerPage { get; set; }
        public double Overlap { get; set; }
        //TODO: Remove default here. doesnt make sense.
        public PageSetup(double footagePerPage = 1000, double overlap = 100)
        {
            UnitsPerPage = footagePerPage;
            Overlap = overlap;
        }

        public List<PageInformation> GetAllPages(double start, double total)
        {
            var numPages = (int)Math.Ceiling(total / UnitsPerPage);
            if (numPages == 0)
            {
                return null;
            }
            var list = new List<PageInformation>();
            for (int i = 0; i < numPages; ++i)
            {
                list.Add(new PageInformation
                {
                    TotalPages = numPages,
                    PageNumber = i + 1,
                    StartFootage = i * UnitsPerPage + start - Overlap,
                    EndFootage = (i + 1) * UnitsPerPage + start + Overlap,
                    Overlap = Overlap
                });
            }
            return list;
        }
    }
}
