using System;
using System.IO;

namespace AxonApiHelper
{
    public class Logger
    {
        public static void Log(string s)
        {
            Console.WriteLine(s);
            File.AppendAllText("output.txt", s + Environment.NewLine);
        }
    }
}
