using AccurateFileSystem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateReportSystem
{
    public class DataMetrics
    {
        public int TotalReads = 0;
        public int TotalAcReads = 0;
        public List<DataMetricRow> On850 = new List<DataMetricRow>();
        public List<DataMetricRow> Off850 = new List<DataMetricRow>();
        public List<DataMetricRow> Off1250 = new List<DataMetricRow>();
        public List<DataMetricRow> OffBetween = new List<DataMetricRow>();
        public List<DataMetricRow> Polarization100 = new List<DataMetricRow>();
        public List<DataMetricRow> Ac = new List<DataMetricRow>();
        public bool UseMir = true;

        public DataMetrics(List<(double, AllegroDataPoint)> onOffData, bool useMir, List<(double Footage, double Value)> polData = null)
        {
            DataMetricRow on850 = null;
            DataMetricRow off850 = null;
            DataMetricRow off1250 = null;
            DataMetricRow offBetween = null;
            DataMetricRow polarization = null;
            UseMir = useMir;

            for (var i = 0; i < onOffData.Count; ++i)
            {
                var (footage, point) = onOffData[i];
                ++TotalReads;
                var curOn = UseMir ? point.MirOn : point.On;
                var curOff = UseMir ? point.MirOff : point.Off;

                if (curOn > -0.85)
                {
                    if (on850 == null)
                    {
                        on850 = new DataMetricRow()
                        {
                            StartFootage = footage,
                            StartPoint = point,
                            EndFootage = footage,
                            EndPoint = point,
                            Readings = 1,
                            Worst = curOn
                        };
                        On850.Add(on850);
                    }
                    else
                    {
                        on850.Readings += 1;
                        on850.EndFootage = footage;
                        on850.EndPoint = point;
                        if (curOn > on850.Worst)
                            on850.Worst = curOn;
                    }
                }
                else if (on850 != null)
                {
                    //On850.Add(on850);
                    on850 = null;
                }
                
                if (curOff > -0.85)
                {
                    if (off850 == null)
                    {
                        off850 = new DataMetricRow()
                        {
                            StartFootage = footage,
                            StartPoint = point,
                            EndFootage = footage,
                            EndPoint = point,
                            Readings = 1,
                            Worst = curOff
                        };
                        Off850.Add(off850);
                    }
                    else
                    {
                        off850.Readings += 1;
                        off850.EndFootage = footage;
                        off850.EndPoint = point;
                        if (curOff > off850.Worst)
                            off850.Worst = curOff;
                    }
                }
                else if (off850 != null)
                {
                    //Off850.Add(off850);
                    off850 = null;
                }

                if (curOff < -1.25)
                {
                    if (off1250 == null)
                    {
                        off1250 = new DataMetricRow()
                        {
                            StartFootage = footage,
                            StartPoint = point,
                            EndFootage = footage,
                            EndPoint = point,
                            Readings = 1,
                            Worst = curOff
                        };
                        Off1250.Add(off1250);
                    }
                    else
                    {
                        off1250.Readings += 1;
                        off1250.EndFootage = footage;
                        off1250.EndPoint = point;
                        if (curOff < off1250.Worst)
                            off1250.Worst = curOff;
                    }
                }
                else if (off1250 != null)
                {
                    //Off1250.Add(off1250);
                    off1250 = null;
                }

                if (curOff < -0.6 && curOff > -0.75)
                {
                    if (offBetween == null)
                    {
                        offBetween = new DataMetricRow()
                        {
                            StartFootage = footage,
                            StartPoint = point,
                            EndFootage = footage,
                            EndPoint = point,
                            Readings = 1,
                            Worst = curOff
                        };
                        OffBetween.Add(offBetween);
                    }
                    else
                    {
                        offBetween.Readings += 1;
                        offBetween.EndFootage = footage;
                        offBetween.EndPoint = point;
                        if (curOff > offBetween.Worst)
                            offBetween.Worst = curOff;
                    }
                }
                else if (offBetween != null)
                {
                    //OffBetween.Add(offBetween);
                    offBetween = null;
                }

                var acReads = point.TestStationReads.Where(ts => ts is ACTestStationRead);
                if (acReads != null && acReads.Count() > 0)
                {
                    foreach (var read in acReads)
                    {
                        var acRead = read as ACTestStationRead;
                        ++TotalAcReads;
                        if (acRead.Value > 2)
                        {
                            Ac.Add(new DataMetricRow()
                            {
                                StartFootage = footage,
                                StartPoint = point,
                                EndFootage = footage,
                                EndPoint = point,
                                Readings = 1,
                                Worst = acRead.Value
                            });
                        }
                    }
                }
            }
            if (polData is null)
                return;
            foreach (var (footage, value) in polData)
            {
                if (value > -0.1)
                {
                    var curPolVal = value;
                    if (polarization == null)
                    {
                        var point = onOffData.First(x => x.Item1 >= footage).Item2;
                        polarization = new DataMetricRow()
                        {
                            StartFootage = footage,
                            StartPoint = point,
                            EndFootage = footage,
                            EndPoint = point,
                            Readings = 1,
                            Worst = curPolVal
                        };
                        Polarization100.Add(polarization);
                    }
                    else
                    {
                        var point = onOffData.First(x => x.Item1 >= footage).Item2;
                        polarization.Readings += 1;
                        polarization.EndFootage = footage;
                        polarization.EndPoint = point;
                        if (curPolVal > polarization.Worst)
                            polarization.Worst = curPolVal;
                    }
                }
                else if (polarization != null)
                {
                    polarization = null;
                }
            }
        }

        public class DataMetricRow
        {
            public double StartFootage;
            public AllegroDataPoint StartPoint;
            public double EndFootage;
            public AllegroDataPoint EndPoint;
            public int Readings;
            public double Worst;
            public double Length => EndFootage - StartFootage;

            public override string ToString()
            {
                return $"{Readings}\t{StartFootage}\t{StartPoint.GPS.Latitude:F8}\t{StartPoint.GPS.Longitude:F8}\t{EndFootage}\t{EndPoint.GPS.Latitude:F8}\t{EndPoint.GPS.Longitude:F8}\t{EndFootage - StartFootage}\t{Worst:F4}";
            }
        }

        public List<(string, string)> GetSheets()
        {
            var sheets = new List<(string, string)>();
            var summary = "Listing\tTotal Reads\tListed Readings\tPercentage\tLength\n";

            var on850 = "Number of Readings\tStart Footage\tStart Latitude\tStart Longitude\tEnd Footage\tEnd Latitude\tEnd Longitude\tLength\tWorst (Volts)\n";
            var curCount = 0;
            var curLength = 0.0;
            foreach (var row in On850)
            {
                curCount += row.Readings;
                curLength += row.Length;
                on850 += row.ToString() + "\n";
            }
            summary += $"On < -0.850V\t{TotalReads}\t{curCount}\t{(curCount / (double)TotalReads):P}\t{curLength}\n";

            var off850 = "Number of Readings\tStart Footage\tStart Latitude\tStart Longitude\tEnd Footage\tEnd Latitude\tEnd Longitude\tLength\tWorst (Volts)\n";
            curCount = 0;
            curLength = 0.0;
            foreach (var row in Off850)
            {
                curCount += row.Readings;
                curLength += row.Length;
                off850 += row.ToString() + "\n";
            }
            summary += $"Off < -0.850V\t{TotalReads}\t{curCount}\t{(curCount / (double)TotalReads):P}\t{curLength}\n";

            var off1250 = "Number of Readings\tStart Footage\tStart Latitude\tStart Longitude\tEnd Footage\tEnd Latitude\tEnd Longitude\tLength\tWorst (Volts)\n";
            curCount = 0;
            curLength = 0.0;
            foreach (var row in Off1250)
            {
                curCount += row.Readings;
                curLength += row.Length;
                off1250 += row.ToString() + "\n";
            }
            summary += $"Off > -1.250V\t{TotalReads}\t{curCount}\t{(curCount / (double)TotalReads):P}\t{curLength}\n";

            var offBetween = "Number of Readings\tStart Footage\tStart Latitude\tStart Longitude\tEnd Footage\tEnd Latitude\tEnd Longitude\tLength\tWorst (Volts)\n";
            curCount = 0;
            curLength = 0.0;
            foreach (var row in OffBetween)
            {
                curCount += row.Readings;
                curLength += row.Length;
                offBetween += row.ToString() + "\n";
            }
            summary += $"Off Between -0.600V and -0.750V\t{TotalReads}\t{curCount}\t{(curCount / (double)TotalReads):P}\t{curLength}\n";

            var pol100 = "Number of Readings\tStart Footage\tStart Latitude\tStart Longitude\tEnd Footage\tEnd Latitude\tEnd Longitude\tLength\tWorst (Volts)\n";
            curCount = 0;
            curLength = 0.0;
            foreach (var row in Polarization100)
            {
                curCount += row.Readings;
                curLength += row.Length;
                pol100 += row.ToString() + "\n";
            }
            summary += $"Polarization < 0.100V\t{TotalReads}\t{curCount}\t{(curCount / (double)TotalReads):P}\t{curLength}\n";

            var ac = "Number of Readings\tStart Footage\tStart Latitude\tStart Longitude\tEnd Footage\tEnd Latitude\tEnd Longitude\tLength\tWorst (Volts)\n";
            curCount = 0;
            curLength = 0.0;
            foreach (var row in Ac)
            {
                curCount += row.Readings;
                curLength += row.Length;
                ac += row.ToString() + "\n";
            }
            summary += $"AC > 2V\t{TotalAcReads}\t{curCount}\t{(curCount / (double)TotalAcReads):P}\t{curLength}\n";

            sheets.Add(("Summary", summary));
            sheets.Add(("On < -0.850V", on850));
            sheets.Add(("Off < -0.850V", off850));
            sheets.Add(("Off > -1.250V", off1250));
            sheets.Add(("Off Between -0.600V and -0.750V", offBetween));
            sheets.Add(("Polarization < 0.100V", pol100));
            sheets.Add(("AC > 2V", ac));

            return sheets;
        }
    }
}
