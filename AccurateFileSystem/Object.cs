using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AccurateFileSystem
{
    public abstract class Object: IEquatable<Object>
    {
        public string Name { get; protected set; }
        public Guid Guid { get; } = Guid.NewGuid();

        public Object(string name)
        {
            Name = name;
        }

        public override int GetHashCode()
        {
            return Guid.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if(obj is Object)
            {
                Object other = obj as Object;
                return Guid.Equals(other.Guid);
            }
            return false;
        }
    }
}
