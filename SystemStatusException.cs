using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KBroker
{
    public class SystemStatusException : Exception
    {
        public SystemStatusException(string message) : base(message) { }
    }
}
