﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateReportSystem
{
    public class PageSetup
    {
        public double FootagePerPage { get; set; }
        public PageSetup(double footagePerPage = 1320)
        {
            FootagePerPage = footagePerPage;
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
                    StartFootage = i * FootagePerPage + startFootage,
                    EndFootage = (i + 1) * FootagePerPage + startFootage
                });
            }
            return list;
        }
    }
}
