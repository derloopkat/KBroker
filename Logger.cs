using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KBroker
{
    public class Logger
    {
        public static void AddEntry(string message)
        {
            try
            {
                var entry = $"{DateTime.Now.ToString("f")} {message} {Environment.NewLine}";
                File.AppendAllText("log.txt", entry);
            }
            catch (Exception ex)
            {
                Display.PrintError("Unable to write log file.");
                Display.PrintCode(ex.Message);
            }
        }
    }
}
