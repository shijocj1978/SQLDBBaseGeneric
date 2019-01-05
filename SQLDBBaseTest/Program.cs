using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Data.SqlClient;
using System.Globalization;
namespace DBBaseTest
{
    class Program
    {
        /* Demo for showing logging in minimum time and without blocking application for huge amount of logging requests.
         * this also shows the application is kept alive even after the last statement in main thread is arrived.
         * Note that no readline or wait are executed in this multithread application.
         */

        static void Main(string[] args)
        {
            //object obj = "23-NOV-15 03.12.19.000000 PM";
            //System.Globalization.CultureInfo provider = CultureInfo.InvariantCulture;
            //String format = String.Format("dd-MMM-yy hh.mm.ss.ffffff tt", obj.ToString().Substring(26, 2));
            //DateTime date = DateTime.ParseExact(obj.ToString(), format, provider);

            Logging.DebugLogEnabled = false;
            OracleDBTest.TestInterface();
            //SQLServerDBTest.TestInterface();
        }

        
    }
}
