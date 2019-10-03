using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public class AllegroCISFile : File
    {
        public Dictionary<string, string> Header { get; private set; }
        public Dictionary<int, AllegroDataPoint> Points { get; private set; }
        public string Extension { get; }
        public AllegroCISFile(string name, string extension, Dictionary<string, string> header, Dictionary<int, AllegroDataPoint> points, FileType type) : base(name, type)
        {
            Header = header;
            Points = points;
            Extension = extension;
            ProcessPoints();
        }

        public override string ToString()
        {
            return $"'{Name}' cis with {Points.Count} points";
        }

        /// <summary>
        /// Function used to clean up the data points in a file. This will also compute the MIR and related data for each point.
        /// This will also clear any duplicated more than once GPS points. May leave many blank GPS points, so you should do something to correct for those.
        /// </summary>
        private void ProcessPoints()
        {

        }

        /// <summary>
        /// Very explicit equals check. Will look at each read and make sure everything is equal in order to reduce the number of duplicate survey files.
        /// This should return true for any of the SVY, CSV, and BAK files from a single survey.
        /// Will also short circuit to true if GUID is equal, without checking anything further.
        /// </summary>
        /// <param name="obj">The other AllegroCISFile.</param>
        /// <returns>A bool stating if the two objects are equal.</returns>
        // TODO: Add a return for what the differences are.
        public override bool IsEquivalent(File otherFile)
        {
            var other = otherFile as AllegroCISFile;
            if (other == null)
                return false;
            // If Guid is equal then we know they are equal. Probably an uncommon check.
            if (other.Guid.Equals(Guid))
                return true;
            if (other.Name != Name)
                return false;
            if (other.Header.Count != Header.Count)
                return false;
            if (other.Points.Count != Points.Count)
                return false;

            // Checking to make sure header key and values match. Since they have the same count you need only check one way.
            foreach (string key in Header.Keys)
                if (!other.Header.ContainsKey(key) || other.Header[key] != Header[key])
                    return false;

            // Checking to make sure point key and values match. Since they have the same count you need only check one way.
            foreach (int key in Points.Keys)
                if (!other.Points.ContainsKey(key) || !Points[key].Equals(other.Points[key]))
                    return false;

            // All of the counts, name, header, and points are equal.
            return true;
        }
    }
}
