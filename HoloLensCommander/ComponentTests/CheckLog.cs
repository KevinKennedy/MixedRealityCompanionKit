using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;


namespace ComponentTests
{
    class CheckLog
    {
        private List<string> logEntries = new List<string>();

        private string LogString
        {
            get
            {
                StringBuilder sb = new StringBuilder();
                foreach (var entry in this.logEntries)
                {
                    sb.Append('{');
                    sb.Append(entry);
                    sb.Append('}');
                }
                return sb.ToString();
            }
        }

        public void Log(string entry)
        {
            this.logEntries.Add(entry);
        }

        public void AssertEquals(string expected)
        {
            this.AssertEquals(true, expected);
        }

        public void AssertEquals(bool actuallyAssert, string expected)
        {
            string actual = this.LogString;

            if (expected != actual)
            {
                var message = "AssertEquals failed.\r\nExpected:\"" + expected + "\"\r\nActual:\"" + actual + "\"\r\n";
                if (actuallyAssert)
                {
                    throw new AssertFailedException(message);
                }
                else
                {
                    Debug.WriteLine(message);
                }
            }

            this.Clear();
        }

        public void Clear()
        {
            this.logEntries.Clear();
        }

        public override string ToString()
        {
            return this.LogString;
        }
    }
}