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
        public double FootagePerPage { get; set; }
        public double Overlap { get; set; }
        //TODO: Remove default here. doesnt make sense.
        public PageSetup(double footagePerPage = 1000, double overlap = 100)
        {
            FootagePerPage = footagePerPage;
            Overlap = overlap;
        }

        

        public List<PageInformation> GetAllPages(double startFootage, double totalFootage)
        {
            int numPages = (int)Math.Ceiling(totalFootage / FootagePerPage);
            if (numPages == 0) return null;
            var list = new List<PageInformation>();
            for (int i = 0; i < numPages; ++i)
            {
                list.Add(new PageInformation
                {
                    TotalPages = numPages,
                    PageNumber = i + 1,
                    StartFootage = i * FootagePerPage + startFootage - Overlap,
                    EndFootage = (i + 1) * FootagePerPage + startFootage + Overlap,
                    Overlap = Overlap
                });
            }
            return list;
        }
    }
}
