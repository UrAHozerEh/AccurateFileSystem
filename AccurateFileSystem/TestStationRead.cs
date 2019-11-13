using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public abstract class TestStationRead
    {
        public string ReplaceId { get; }
        public string OriginalString { get; }
        public string Tag { get; protected set; }

        public TestStationRead(string original, string replaceId)
        {
            ReplaceId = replaceId;
            OriginalString = original;
        }
    }

    public class TestStationReadFactory
    {
        public static TestStationRead GetRead(string value, string replaceId)
        {
            if (ACTestStationRead.IsMatch(value))
                return new ACTestStationRead(value, replaceId);
            if (SingleTestStationRead.IsMatch(value))
                return new SingleTestStationRead(value, replaceId);
            if (SideDrainTestStationRead.IsMatch(value))
                return new SideDrainTestStationRead(value, replaceId);
            if (ReconnectTestStationRead.IsMatch(value))
                return new ReconnectTestStationRead(value, replaceId);
            if (CasingTestStationRead.IsMatch(value))
                return new CasingTestStationRead(value, replaceId);
            if (InsulationTestStationRead.IsMatch(value))
                return new InsulationTestStationRead(value, replaceId);
            if (CrossingTestStationRead.IsMatch(value))
                return new CrossingTestStationRead(value, replaceId);
            return null;
        }
    }

    public class ACTestStationRead : TestStationRead
    {
        public static string RegexPattern => @"\(ACV([^:]+)?: ([^V]+)V\)";
        public double Value { get; }
        public ACTestStationRead(string original, string replaceId) : base(original, replaceId)
        {
            Match match = Regex.Match(original, RegexPattern);
            if (!match.Success)
                throw new Exception();
            if (match.Groups.Count == 3)
                Tag = match.Groups[1].Value;
            Value = double.Parse(match.Groups[match.Groups.Count - 1].Value);
        }
        public static bool IsMatch(string value)
        {
            return Regex.IsMatch(value, RegexPattern);
        }
    }

    public class SingleTestStationRead : TestStationRead
    {
        public static string OnOffRegexPattern => @"\(Single([^:]+)?:(.*)P/S ([^V]+)V IRF ([^V]+)V\)";
        public static string OnRegexPattern => @"\(Single([^:]+)?: Struct P/S ([^V]+)V\)";
        public bool IsOnOff { get; }
        public double On { get; }
        public double Off { get; }
        public string Title { get; }
        public SingleTestStationRead(string original, string replaceId) : base(original, replaceId)
        {
            Match onOffMatch = Regex.Match(original, OnOffRegexPattern);
            Match onMatch = Regex.Match(original, OnRegexPattern);
            if (onOffMatch.Success)
            {
                IsOnOff = true;
                int count = onOffMatch.Groups.Count;
                if (count == 5)
                    Tag = onOffMatch.Groups[1].Value;
                Title = onOffMatch.Groups[count - 3].Value.Trim();
                On = double.Parse(onOffMatch.Groups[count - 2].Value);
                Off = double.Parse(onOffMatch.Groups[count - 1].Value);
            }
            else if (onMatch.Success)
            {
                IsOnOff = false;
            }
            else
                throw new Exception();
        }
        public static bool IsMatch(string value)
        {
            return Regex.IsMatch(value, OnOffRegexPattern) || Regex.IsMatch(value, OnRegexPattern);
        }
    }

    public class SideDrainTestStationRead : TestStationRead
    {
        public static string OnOffRegexPattern => @"\(Side drain([^:]+)?: Left P/S ([^V]+)V IRF ([^V]+)V, Right P/S ([^V]+)V IRF ([^V]+)V\)";
        public static string OnRegexPattern => @"\(Side drain([^:]+)?: Left P/S ([^V]+)V, Right P/S ([^V]+)V\)";
        public bool IsOnOff { get; }
        public double LeftOn { get; }
        public double LeftOff { get; }
        public double RightOn { get; }
        public double RightOff { get; }

        public SideDrainTestStationRead(string original, string replaceId) : base(original, replaceId)
        {
            Match onOffMatch = Regex.Match(original, OnOffRegexPattern);
            Match onMatch = Regex.Match(original, OnRegexPattern);
            if (onOffMatch.Success)
            {
                IsOnOff = true;
                int count = onOffMatch.Groups.Count;
                if (count == 6)
                    Tag = onOffMatch.Groups[1].Value;
                LeftOn = double.Parse(onOffMatch.Groups[count - 4].Value);
                LeftOff = double.Parse(onOffMatch.Groups[count - 3].Value);

                RightOn = double.Parse(onOffMatch.Groups[count - 2].Value);
                RightOff = double.Parse(onOffMatch.Groups[count - 1].Value);
            }
            else if (onMatch.Success)
            {
                IsOnOff = false;
            }
            else
                throw new Exception();
        }

        public static bool IsMatch(string value)
        {
            return Regex.IsMatch(value, OnOffRegexPattern) || Regex.IsMatch(value, OnRegexPattern);
        }
    }

    public class ReconnectTestStationRead : TestStationRead
    {
        public static string OnOffRegexPattern => @"\(TPR([^:]+)?:FG ([^V]+)V/([^V]+)V, MIR ([^V]+)V/([^V]+)V, NG ([^V]+)V/([^V]+)V?\)";
        public static string OnRegexPattern => @"\(TPR([^:]+)?:FG ([^V]+)V, MIR ([^V]+)V, NG ([^V]+)V\)";
        public bool IsOnOff { get; }
        public double FGOn { get; }
        public double FGOff { get; }
        public double MirOn { get; }
        public double MirOff { get; }
        public double NGOn { get; }
        public double NGOff { get; }
        public double ReconnectDistance => Math.Abs(EndPoint.Footage - StartPoint.Footage);
        public double MirOnPerFoot => MirOn / ReconnectDistance;
        public double MirOffPerFoot => MirOff / ReconnectDistance;
        public AllegroDataPoint StartPoint { get; set; }
        public AllegroDataPoint EndPoint { get; set; }

        public ReconnectTestStationRead(string original, string replaceId) : base(original, replaceId)
        {
            Match onOffMatch = Regex.Match(original, OnOffRegexPattern);
            Match onMatch = Regex.Match(original, OnRegexPattern);
            if (onOffMatch.Success)
            {
                IsOnOff = true;
                int count = onOffMatch.Groups.Count;
                if (count == 8)
                    Tag = onOffMatch.Groups[1].Value;
                FGOn = double.Parse(onOffMatch.Groups[count - 6].Value);
                FGOff = double.Parse(onOffMatch.Groups[count - 5].Value);

                MirOn = double.Parse(onOffMatch.Groups[count - 4].Value);
                MirOff = double.Parse(onOffMatch.Groups[count - 3].Value);

                NGOn = double.Parse(onOffMatch.Groups[count - 2].Value);
                NGOff = double.Parse(onOffMatch.Groups[count - 1].Value);
            }
            else if (onMatch.Success)
            {
                IsOnOff = false;
            }
            else
                throw new Exception();
        }

        public static bool IsMatch(string value)
        {
            return Regex.IsMatch(value, OnOffRegexPattern) || Regex.IsMatch(value, OnRegexPattern);
        }
    }

    public class CasingTestStationRead : TestStationRead
    {
        public static string OnOffRegexPattern => @"\(Line/casing([^:]+)?: Struct P/S ([^V]+)V IRF ([^V]+)V, Casing P/S ([^V]+)V IRF ([^V]+)V\)";
        public static string OnRegexPattern => @"\(Line/casing([^:]+)?: Struct P/S ([^V]+)V, Casing P/S ([^V]+)V\)";
        public bool IsOnOff { get; }
        public double StructOn { get; }
        public double StructOff { get; }
        public double CasingOn { get; }
        public double CasingOff { get; }

        public CasingTestStationRead(string original, string replaceId) : base(original, replaceId)
        {
            Match onOffMatch = Regex.Match(original, OnOffRegexPattern);
            Match onMatch = Regex.Match(original, OnRegexPattern);
            if (onOffMatch.Success)
            {
                IsOnOff = true;
                int count = onOffMatch.Groups.Count;
                if (count == 6)
                    Tag = onOffMatch.Groups[1].Value;
                StructOn = double.Parse(onOffMatch.Groups[count - 4].Value);
                StructOff = double.Parse(onOffMatch.Groups[count - 3].Value);

                CasingOn = double.Parse(onOffMatch.Groups[count - 2].Value);
                CasingOff = double.Parse(onOffMatch.Groups[count - 1].Value);
            }
            else if (onMatch.Success)
            {
                IsOnOff = false;
            }
            else
                throw new Exception();
        }

        public static bool IsMatch(string value)
        {
            return Regex.IsMatch(value, OnOffRegexPattern) || Regex.IsMatch(value, OnRegexPattern);
        }
    }

    public class CrossingTestStationRead : TestStationRead
    {
        public static string OnOffRegexPattern => @"\(Crossing without bond([^:]+)?: Struct P/S ([^V]+)V IRF ([^V]+)V, Forgn P/S ([^V]+)V IRF ([^V]+)V\)";
        public static string OnRegexPattern => @"\(Crossing without bond([^:]+)?: Struct P/S ([^V]+)V, Forgn P/S ([^V]+)V\)";
        public bool IsOnOff { get; }
        public double StructOn { get; }
        public double StructOff { get; }
        public double ForeignOn { get; }
        public double ForeignOff { get; }

        public CrossingTestStationRead(string original, string replaceId) : base(original, replaceId)
        {
            Match onOffMatch = Regex.Match(original, OnOffRegexPattern);
            Match onMatch = Regex.Match(original, OnRegexPattern);
            if (onOffMatch.Success)
            {
                IsOnOff = true;
                int count = onOffMatch.Groups.Count;
                if (count == 6)
                    Tag = onOffMatch.Groups[1].Value;
                StructOn = double.Parse(onOffMatch.Groups[count - 4].Value);
                StructOff = double.Parse(onOffMatch.Groups[count - 3].Value);

                ForeignOn = double.Parse(onOffMatch.Groups[count - 2].Value);
                ForeignOff = double.Parse(onOffMatch.Groups[count - 1].Value);
            }
            else if (onMatch.Success)
            {
                IsOnOff = false;
            }
            else
                throw new Exception();
        }

        public static bool IsMatch(string value)
        {
            return Regex.IsMatch(value, OnOffRegexPattern) || Regex.IsMatch(value, OnRegexPattern);
        }
    }
    //(Crossing without bond: Struct P/S -1.292V IRF -1.002V, Forgn P/S -1.287V IRF -0.996V)

    public class InsulationTestStationRead : TestStationRead
    {
        public static string OnOffRegexPattern => @"\(Insulation TP([^:]+)?: Struct P/S ([^V]+)V IRF ([^V]+)V, Forgn P/S ([^V]+)V IRF ([^V]+)V\)";
        public static string OnRegexPattern => @"\(Insulation TP([^:]+)?: Struct P/S ([^V]+)V, Forgn P/S ([^V]+)V\)";
        public bool IsOnOff { get; }
        public double StructOn { get; }
        public double StructOff { get; }
        public double ForeignOn { get; }
        public double ForeignOff { get; }

        public InsulationTestStationRead(string original, string replaceId) : base(original, replaceId)
        {
            Match onOffMatch = Regex.Match(original, OnOffRegexPattern);
            Match onMatch = Regex.Match(original, OnRegexPattern);
            if (onOffMatch.Success)
            {
                IsOnOff = true;
                int count = onOffMatch.Groups.Count;
                if (count == 6)
                    Tag = onOffMatch.Groups[1].Value;
                StructOn = double.Parse(onOffMatch.Groups[count - 4].Value);
                StructOff = double.Parse(onOffMatch.Groups[count - 3].Value);

                ForeignOn = double.Parse(onOffMatch.Groups[count - 2].Value);
                ForeignOff = double.Parse(onOffMatch.Groups[count - 1].Value);
            }
            else if (onMatch.Success)
            {
                IsOnOff = false;
            }
            else
                throw new Exception();
        }

        public static bool IsMatch(string value)
        {
            return Regex.IsMatch(value, OnOffRegexPattern) || Regex.IsMatch(value, OnRegexPattern);
        }
    }
    //(Insulation TP: Struct P/S -0.984V IRF -0.931V, Forgn P/S -0.987V IRF -0.947V)
}
