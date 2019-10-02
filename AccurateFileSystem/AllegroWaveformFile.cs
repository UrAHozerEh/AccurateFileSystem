using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    /// <summary>
    /// This is the txt file that is exported with a typical CIS from the allegro. It is created in the AiDVM software.
    /// Nothing should be mutable once the file is read.
    /// </summary>
    public class AllegroWaveformFile : File
    {
        /// <summary>
        /// The time the file was recorded. Since the Allegro does not store the seconds, we can not use this to
        /// determine exactly when each individual reading was. We can at most assume we are within one minute of the actual time.
        /// </summary>
        public DateTime Time { get; }
        /// <summary>
        /// The number of reads per second that are recorded in the file.
        /// </summary>
        public int SampleRate { get; }
        /// <summary>
        /// The DVM range setting.
        /// </summary>
        public string Range { get; }
        /// <summary>
        /// The remark typed into the Allegro.
        /// </summary>
        public string Remark { get; }
        /// <summary>
        /// List of all the data points.
        /// </summary>
        public IReadOnlyList<DataPoint> Points { get; }
        /// <summary>
        /// The total time the file covers. Computed by "number of points / sample rate". Should probably always be a whole number,
        /// but using double just in case.
        /// </summary>
        public double TotalSeconds => ((double)Points.Count) / SampleRate;


        public AllegroWaveformFile(string name, List<DataPoint> points, DateTime time, int sampleRate, string range, string remark): base(name, FileType.CISWaveform)
        {
            Points = points.AsReadOnly();
            Time = time;
            SampleRate = sampleRate;
            Range = range;
            Remark = remark;
        }

        public override string ToString()
        {
            return $"'{Name}' waveform for {Points.Count / SampleRate} seconds";
        }

        /// <summary>
        /// Stores the data that is recorded after the header in the waveform files. Each line has a double reading
        /// followed by either 3 boolean values if the GPS is synced, or nothing if it is not. The HasInterruptorData
        /// bool is used to determine if the other bools have actual values or are just defaults.
        /// </summary>
        public readonly struct DataPoint
        {
            /// <summary>
            /// The pipe-to-soil voltage reading.
            /// </summary>
            public readonly double Value;
            /// <summary>
            /// True if the value is during the On cycle of interruption.
            /// </summary>
            public readonly bool IsOn;
            /// <summary>
            /// Unsure on value. Best guess is it is true for the value that would be recorded if it were a survey file, wether it be On or Off value.
            /// </summary>
            public readonly bool Second;
            /// <summary>
            /// Unsure on value. Best guess is it toggles to true at the top of a second.
            /// </summary>
            public readonly bool Third;
            /// <summary>
            /// True if there was the boolean data showing interruptor sync.
            /// </summary>
            public readonly bool HasInterruptorData;

            public DataPoint(double value)
            {
                Value = value;
                IsOn = false;
                Second = false;
                Third = false;
                HasInterruptorData = false;
            }

            public DataPoint(double value, bool isOn, bool second, bool third)
            {
                Value = value;
                IsOn = isOn;
                Second = second;
                Third = third;
                HasInterruptorData = true;
            }
        }
    }
}
