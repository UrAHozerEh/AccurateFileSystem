using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateReportSystem.AccurateDrawingDevices
{
    public class AccurateTextFormat
    {
        public AccurateFont Font { get; set; } = AccurateFont.Arial;
        public float FontSize { get; set; } = 12;
        public AccurateWordWrapping WordWrapping { get; set; } = AccurateWordWrapping.WholeWord;
        public AccurateAlignment HorizontalAlignment { get; set; } = AccurateAlignment.Center;
        public AccurateAlignment VerticalAlignment { get; set; } = AccurateAlignment.Center;
        public AccurateFontWeight FontWeight { get; set; } = AccurateFontWeight.Regular;
    }

    public enum AccurateWordWrapping
    {
        NoWrap,
        WholeWord
    }

    public enum AccurateAlignment
    {
        Start,
        Center,
        End
    }

    public enum AccurateFont
    {
        Arial
    }

    public enum AccurateFontWeight
    {
        Regular,
        Bold,
        Thin
    }
}
