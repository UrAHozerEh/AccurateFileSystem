using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem.Xml
{
    public class XmlObject
    {
        public string Name { get; set; }
        public string Value { get; set; }
        public Dictionary<string, string> Settings { get; set; }
        public List<XmlObject> Children { get; set; }

        public XmlObject(string name, string value, Dictionary<string, string> settings, List<XmlObject> children)
        {
            Name = name;
            Value = value;
            Settings = settings;
            Children = children;
        }

        public XmlObject(string name, string value = "")
        {
            Name = name;
            Value = value;
            Settings = new Dictionary<string, string>();
            Children = new List<XmlObject>();
        }
        
        public string GetFileString(int level = 0)
        {
            var output = new StringBuilder();
            var tabs = "".PadLeft(level, '\t');
            output.Append($"{tabs}<{Name}{GetSettingsString()}>");
            if(Children.Count == 0)
            {
                output.AppendLine($"{GetSafeValue()}</{Name}>");
            }
            else
            {
                output.AppendLine("");
                foreach(var child in Children)
                {
                    output.AppendLine(child.GetFileString(level + 1));
                }
                output.Append($"{tabs}</{Name}>");
            }

            return output.ToString();
        }

        private string GetSafeValue()
        {
            var output = Value.Replace("&", "&amp;");
            return output;
        }

        public string GetSettingsString()
        {
            var output = new StringBuilder();
            foreach (var (key, value) in Settings)
            {
                output.Append($" {key}=\"{value}\"");
            }
            return output.ToString();
        }

        public List<XmlObject> GetObjects(string name)
        {
            if (name.Trim() == Name.Trim())
                return new List<XmlObject>() { this };

            var output = new List<XmlObject>();
            
            foreach(var obj in Children)
            {
                var curObjs = obj.GetObjects(name);
                output.AddRange(curObjs);
            }
            return output;
        }
    }
}
