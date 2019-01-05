using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace LoggerTest
{
    class Program
    {
        #region LogggingTest

           
            /*Logging Test*/
            static void Main(string[] args)
            {
                File.Delete(Logging.ExceptionFilePath); //delete log files 
                Logging.AddExceptionEntry(new Exception("New custom Exception"), "Custom Exception Header", args);
                ExceptionHandlerCase1();
                ExceptionHandlerCase2("f", "l", 200);
                Console.WriteLine("Start Heavy LoadTest");
                Console.ReadLine();
                Logging.WaitForLogComplete();
                LogLoadTest();
                Console.WriteLine(Logging.GetExceptionString(new Exception("Testing Exception"), "Test Header"));
                Console.WriteLine(Logging.GetExceptionString(new Exception("Testing Exception"), "Test Header", args));
                Logging.AddLogEntry(Logging.GetLogString("Testing Exception0", "Test Header0"));
                Logging.AddLogEntry(Logging.GetLogString("Testing Exception1", "Test Header1", MethodBase.GetCurrentMethod(), args));
                Logging.AddDebugEntry("Testing Exception", "Test Header");
                Logging.AddLogEntry("Testing Exception", "Test Header");
                Console.WriteLine("UnhandledExceptionTest start");
                Logging.AddDebugEntry("msg", "header");
                Console.WriteLine("Wait for application pending logs");
                Logging.WaitForLogComplete();
                Logging.WriteToOSApplicationLog(new Exception("OSD Application Log"), EventLogEntryType.Error, args);
                Logging.WriteToOSApplicationLog("OSD MESSAGE", EventLogEntryType.Error, MethodBase.GetCurrentMethod(), args);
                Logging.AddExceptionEntry(new Exception("New custom Exception24"), "Custom Exception Header24", args);
                Console.WriteLine("Press Enter key for UnhandledException Test");
                Console.WriteLine("find the logs in teh location:" + Logging.ExceptionFilePath);
                Console.ReadLine();
                UnhandledExceptionTest(new object());
            }

            static void LogLoadTest()
            {
                int i = 0;
                for (i = 0; i < 15550; i++)
                {
                    Logging.AddExceptionEntry(new Exception("Hello Exception " + i), "Header " + i, DebugLogActionEnum.Disabled);
                }
            }

            static void UnhandledExceptionTest(object args)
            {
                Logging.AddLogEntry("End");
                throw new Exception("Test");
            }

            static void ExceptionHandlerCase1()
            {
                ExceptionHandlerx();
            }
            static void ExceptionHandlerx()
            {
                ExceptionHandler("f", "l", 200);
            }


            static void ExceptionHandler(string firstname,string lastname,int age)
            {
                try
                {
                    throw new ArgumentOutOfRangeException("dummy", 200, "Expect less that 130");
                }
                catch (Exception ex)
                {
                    Logging.AddExceptionEntry(ex, "Exception on ExceptionHandlerCase1", firstname, lastname, age);
                }
            }

            static void ExceptionHandlerCase2(string firstname, string lastname, int age)
            {
                try
                {
                    ExceptionHandlerRx(firstname, lastname, age);
                }
                catch (Exception ex)
                {
                    Logging.AddExceptionEntry(ex, "Exception on ExceptionHandlerCase2", firstname, lastname, age);
                }
            }
            static void ExceptionHandlerRx(string firstname, string lastname, int age)
            {
                ExceptionHandlerR(firstname,lastname,age);
            }
            static void ExceptionHandlerR(string firstname, string lastname, int age)
            {
                throw new ArgumentOutOfRangeException("dummy", 200, "Expect less that 130");
            }

    #endregion    
} 
}
