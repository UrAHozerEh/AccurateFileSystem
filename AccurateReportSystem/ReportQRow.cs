using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateReportSystem
{
    public class ReportQRow
    {
        public string HcaId { get; set; }
        public string Route { get; set; }
        public string StartMp { get; set; }
        public string EndMp { get; set; }
        public string Region { get; set; }
        public double StartFootage { get; set; }
        public string StartStationing => ToStationing(StartFootage);
        public double EndFootage { get; set; }
        public string EndStationing => ToStationing(EndFootage);
        public double Length => EndFootage - StartFootage;
        public string DoC { get; set; } = "NT";
        public double StartLat { get; set; }
        public double StartLon { get; set; }
        public double EndLat { get; set; }
        public double EndLon { get; set; }
        public string FirstTime { get; set; }
        public string Cis { get; set; } = "NT";
        public string Dcvg { get; set; } = "NT";
        public string Acvg { get; set; } = "NT";
        public string Pcm { get; set; } = "NT";
        public string Priority { get; set; } = "NT";
        public string Comment { get; set; } = "";

        public string ToExcel(bool includeDcvg, bool includeAcvg)
        {
            var output = new StringBuilder();

            output.Append($"{HcaId}\t{Route}\t{StartMp}\t{EndMp}\t{StartStationing}\t{EndStationing}\t{Region}\t{Length}\t{DoC}\t");
            output.Append($"{StartLat:F8}\t{StartLon:F8}\t{EndLat:F8}\t{EndLon:F8}\t{FirstTime}\t{Cis}\t");
            if (includeDcvg)
            {
                output.Append($"{Dcvg}\t");
            }
            if (includeAcvg)
            {
                output.Append($"{Acvg}\t");
            }
            output.Append($"{Pcm}\t{Priority}\t{Comment}");
            return output.ToString();
        }

        private static string ToStationing(double footage)
        {
            var hundred = (int)footage / 100;
            var tens = (int)footage % 100;
            return hundred.ToString().PadLeft(1, '0') + "+" + tens.ToString().PadLeft(2, '0');
        }
    }
}
