using System;

namespace KBroker
{
    public class SystemStatusException : Exception
    {
        public SystemStatusException(string message) : base(message) { }
    }

    public class ConfigurationException : Exception
    {
        public ConfigurationException(string message) : base(
            $"Error reading {Configuration.OrdersFileName} file. "
            + "Make sure syntax is correct and numbers don't include comma as thousands separator."
            + Environment.NewLine + message )
        {
   
        }
    }


}
