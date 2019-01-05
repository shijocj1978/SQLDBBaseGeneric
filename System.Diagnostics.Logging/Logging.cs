using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using System.Web;

namespace System.Diagnostics
{
    /// <summary>
    /// <para>Provides a quick implemetation of File logging which focuses on zero learning time and performance. </para>
    /// <para>Use as a quick logging for your utility app. The logging class can be used as simple as using Logging.AddExceptionEntry(..,..).</para>
    /// <para>Will work with default settings on the module. If required logging features can be further customized by adding entries in app config </para>
    /// <para>on entry point executable's App.config file and Logging module will pick them automatically or it can be added runtime via code.</para>
    /// <para>Main Features: (1) Built to work seamlessly with huge amount of logs implementing multiple Threads. (2) Auto logging for unhandled Exceptions. </para>
    /// <para>(3) Assist user to make sure all logs are written even on uncaught exceptions. (4) Failsafe logging capabilities- Tries to write to OS Application Event Log. </para>
    /// <para>(5) Standalone Exception parser with timestamp. (6) Capable of logging parameter values from method the exception is caught. (7) Plug and play. Work on preset settings</para>
    /// <para>----------------------------------------------------------------------------------------------------------------------------------------------------</para>
    /// <para>    DEFAULT VALUES WHICH CAN BE MODIFIED BY ADDING ENTRIES TO APP.CONFIG-> APPSETTINGS SECTION</para>
    /// <para>----------------------------------------------------------------------------------------------------------------------------------------------------</para>
    /// <para></para>
    /// <para> LogFilePath: Default value = [Users %AppData%\[Application executable Name]\AppLogs.log.] USAGE:  Give full path or relative path with file name.</para>
    /// <para> DebugFilePath: Default value = [Users %AppData%\[Application executable Name]\AppLogs.log.] USAGE:  Give full path or relative path with file name.</para>
    /// <para> ExceptionFilePath: Default value = [Users %AppData%\[Application executable Name]\AppLogs.log.] USAGE:  Give full path or relative path with file name.</para>
    /// <para> DebugLogEnabled: Default value = [True] USAGE: if true, enable Debuglogging which activates AddDebugEntry(..).</para>
    /// <para> WindowsEventsSectionName: Default value = [Application executable Name] USAGE: If provided in app.config, creates a section with name specfied in windows eventlog</para>
    /// <para> FileWriteWaitTimeoutSeconds: Default value = [120 Seconds] USAGE: If provided in app.config, creates a section with name specfied in windows eventlog</para>
    /// </summary>
    public static class Logging
    {
        static string _LogFilepath, _ExceptionFilePath, _DebugFilePath;
        static bool _DebugLogEnabled;
        static string _WindowsEventsSectionName;
        static int _FileWriteWaitTimeoutSeconds;
        static AppDomain currentDomain;
        static System.Collections.Concurrent.ConcurrentDictionary<int, Task> tasks = new System.Collections.Concurrent.ConcurrentDictionary<int, Task>();


        /// <summary>
        /// Gets or Sets LogFilePath. Default value = [Users %AppData%\[Application executable Name]\AppLogs.log.] USAGE:  Create an Appconfig ->AppSettings->'LogFilePath'  entry and give full or relative path with file name.
        /// </summary>
        public static string LogFilepath
        {
            get { return _LogFilepath; }
            set { _LogFilepath = value; }
        }

        /// <summary>
        /// Gets or Sets ExceptionFilePath. Default value = [Users %AppData%\[Application executable Name]\AppLogs.log.] USAGE:  Create an Appconfig ->AppSettings->'ExceptionFilePath'  entry and give full or relative path with file name.
        /// </summary>
        public static string ExceptionFilePath
        {
            get { return _ExceptionFilePath; }
            set { _ExceptionFilePath = value; }
        }

        /// <summary>
        /// Gets or Sets DebugFilePath. Default value = [Users %AppData%\[Application executable Name]\AppLogs.log.] USAGE:  Create an Appconfig ->AppSettings->'DebugFilePath'  entry and give full or relative path with file name.
        /// </summary>
        public static string DebugFilePath
        {
            get { return _DebugFilePath; }
            set { _DebugFilePath =value; }
        }

        /// <summary>
        /// Gets or sets Debuglogging parameter to enable or disable Debug logging dynamically. By default this will be enabled in all cases except release mode. Manual override is possible via app config or via coding.
        /// </summary>
        public static bool DebugLogEnabled
        {
            get { return _DebugLogEnabled; }
            set { _DebugLogEnabled = value; }
        }

        /// <summary>
        /// Gets or sets FileWriteWaitTimeoutSeconds parameter to change Debug logging file timeoutseconds. Default is 120 seonds where thread will retry every 1/2 second until timeout expires. App config configuration is also available.
        /// </summary>
        public static int FileWriteWaitTimeoutSeconds
        {
            get { return _FileWriteWaitTimeoutSeconds; }
            set { _FileWriteWaitTimeoutSeconds = value; }
        }

        static Logging()
        {
            if (ConfigurationManager.AppSettings.AllKeys.Contains("LogFilePath") == true)
            {
                try
                {
                    _LogFilepath = ConfigurationManager.AppSettings["LogFilePath"].ToString();
                    if (Path.IsPathRooted(_LogFilepath) == false)
                        _LogFilepath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" +
                            Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName.ToString()) + _LogFilepath;
                    if (Directory.Exists(Path.GetDirectoryName(_LogFilepath)) == false)
                        Directory.CreateDirectory(Path.GetDirectoryName(_LogFilepath));
                    File.Create(_LogFilepath); //This checks the path is valid, permissions are there and also any other exceptions. If there is an exception in any of the above stage then throw an exception.
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("LogFilePath value in config is invalid or got permission issues. Check the directory and filename is valid. Please specify a valid absolute or relative path with filename", ex);
                }
            }
            else
                _LogFilepath = GetDefaultPath();

            if (ConfigurationManager.AppSettings.AllKeys.Contains("ExceptionFilePath") == true)
            {
                try
                {
                    _ExceptionFilePath = ConfigurationManager.AppSettings["ExceptionFilePath"].ToString();
                    if (Path.IsPathRooted(_ExceptionFilePath) == false)
                        _ExceptionFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + 
                            Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName.ToString()) + _ExceptionFilePath;
                    if (Directory.Exists(Path.GetDirectoryName(_ExceptionFilePath)) == false)
                        Directory.CreateDirectory(Path.GetDirectoryName(_ExceptionFilePath));
                    File.Create(_ExceptionFilePath); //This checks the path is valid, permissions are there and also any other exceptions. If there is an exception in any of the above stage then throw an exception.
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("ExceptionFilePath value in config is invalid or got permission issues. Check the directory and filename is valid. Please specify a valid absolute or relative path with filename", ex);
                }
            }
            else
                _ExceptionFilePath = GetDefaultPath();

            if (ConfigurationManager.AppSettings.AllKeys.Contains("DebugFilePath") == true)
            {
                try
                {
                    _DebugFilePath = ConfigurationManager.AppSettings["DebugFilePath"].ToString();
                    if (Path.IsPathRooted(_DebugFilePath) == false)
                        _DebugFilePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" + 
                            Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName.ToString()) + _DebugFilePath;
                    if (Directory.Exists(Path.GetDirectoryName(_DebugFilePath)) == false)
                    Directory.CreateDirectory(Path.GetDirectoryName(_DebugFilePath));
                    File.Create(_DebugFilePath); //This checks the path is valid, permissions are there and also any other exceptions. If there is an exception in any of the above stage then throw an exception.
                }
                catch (Exception ex)
                {
                    throw new ArgumentException("DebugFilePath value in config is invalid or got permission issues. Check the directory and filename is valid. Please specify a valid absolute or relative path with filename", ex);
                }
            }
            else
                _DebugFilePath = GetDefaultPath();

            if (ConfigurationManager.AppSettings.AllKeys.Contains("DebugLogEnabled") == true)
            {
                if (bool.TryParse(ConfigurationManager.AppSettings["DebugLogEnabled"].ToString(), out _DebugLogEnabled) == false)
                    throw new ArgumentException("Error while parsing configuration value Appsettings->DebugLogEnabled. Please check the value.");
            }
            else
            {
                if (System.Diagnostics.Debugger.IsAttached == true)
                    _DebugLogEnabled = true; //enable debug info by default if running from VS IDE.
                else
                {
                    #if DEBUG
                        _DebugLogEnabled = true; //enables debug info if in debug mode compile.
                    #else
                        _DebugLogEnabled = false; //disable debug if running in optimised mode. At this stage only manual enable of debug will give results.
                    #endif
                }
            }
            if (ConfigurationManager.AppSettings.AllKeys.Contains("WindowsEventsSectionName") == true)
                _WindowsEventsSectionName = ConfigurationManager.AppSettings["WindowsEventsSectionName"].ToString();
            else
                _WindowsEventsSectionName = Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName.ToString());

            if (ConfigurationManager.AppSettings.AllKeys.Contains("FileWriteWaitTimeoutSeconds") == true)
                if (int.TryParse(ConfigurationManager.AppSettings["FileWriteWaitTimeoutSeconds"].ToString(), out _FileWriteWaitTimeoutSeconds) == false)
                    throw new ArgumentException("Error while parsing configuration value Appsettings->FileWriteWaitTimeoutSeconds. Please check the value.");
                else
                    _FileWriteWaitTimeoutSeconds = 120;
            //Try to handle unhandled exceptions from user.
            currentDomain = AppDomain.CurrentDomain;
            currentDomain.UnhandledException += currentDomain_UnhandledException;
        }

        public static string GetDefaultPath()
        {
            string _path = "";
            try
            {
                //find the assembly is refered to ASP.net or windows/console/WCF etc. Ref: https://msdn.microsoft.com/library/ms241730(v=vs.100).aspx (How to: Find the Name of the ASP.NET Process)
                if (System.Diagnostics.Process.GetCurrentProcess().MainModule.ModuleName == "w3wp.exe"
                    || System.Diagnostics.Process.GetCurrentProcess().MainModule.ModuleName == "aspnet_wp.exe"
                    || System.Diagnostics.Process.GetCurrentProcess().MainModule.ModuleName == "iisexpress.exe")
                {
                    _path = System.Web.HttpContext.Current.Server.MapPath("~/") + "Logs\\"; 
                    Directory.CreateDirectory(_path);
                    _path += "AppLogs.log";
                }
                else
                {
                    _path = System.Diagnostics.Process.GetCurrentProcess().ProcessName + Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\" +
                        Path.GetFileName(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName.ToString()) + "\\AppLogs.log";
                }
            }
            catch 
            {
                throw; 
            }
            return _path;
        }

        static void currentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is System.Exception)
            {
                //write it as syncronized mode as this may require the highest priority.
                 MessageEntryEnclave.AddExceptionEntryAsync((Exception)e.ExceptionObject, "UNHANDLED EXCPECTION CAUGHT BY LOGGING BLOCK: CONTACT TECH SUPPORT IF YOU SEE THIS FREQUENTLY" + (e.IsTerminating == true ? " AND APPLICATION IS CRASHING" : "") + ".");
            }
            else
                //write it as syncronized mode as this may require the highest priority.
                 MessageEntryEnclave.AddLogEntryAsync("UNHANDLED EXCPECTION CAUGHT BY LOGGING BLOCK: PLEASE NOTE THAT THE TYPE OF EXCEPTION OBJECT IS NOT COMING UNDER 'SYSTEM.EXCEPTION'. CONTACT TECH SUPPORT IF YOU SEE THIS FREQUENTLY" + (e.IsTerminating == true ? " AND APPLICATION IS CRASHING" : "") + ".");
        }


        /// <summary>
        /// Add Exception record to logging file. Note that this is an Asynchronous operation.
        /// Refer to WaitForLogComplete() is you have too much of data to be written or application lifetime issues on logging.
        /// </summary>
        /// <param name="ex">Exception object</param>
        /// <param name="header">Header information or any additional information which can add to exception troubleshooting.</param>
        /// <param name="methodvalues">Values that are passed into current method which try catch is written/caught. The param array must be in the same order of method parameters. If exception happens in a child method then these values can be picked from logs and used as a starting point of debug.</param>
        public static void AddExceptionEntry(Exception ex, string header = "", params object[] methodvalues)
        {
            Task t = Task.Run(() => MessageEntryEnclave.AddExceptionEntryAsync(ex, header, methodvalues));
            RemoveCurrentTask(t);
        }

        /// <summary>
        /// Add log record to logging file. Note that this is an Asynchronous operation.
        /// Refer to WaitForLogComplete() is you have too much of data to be written or application lifetime issues on logging.
        /// </summary>
        /// <param name="message">Message to log into the file</param>
        /// <param name="header">Header information or any additional information which can add to exception troubleshooting.</param>
        /// <param name="method">Get an instance of current Method. Use 'MethodBase.GetCurrentMethod()' to get the current method. If value is not passed or null methodvalues will be ignored.</param>
        /// <param name="methodvalues">Values that are passed into current method which try catch is written/caught. The param array must be in the same order of method parameters. If exception happens in a child method then these values can be picked from logs and used as a starting point of debug.</param>
        public static void AddLogEntry(string message, string header = "", MethodBase method = null, params object[] methodvalues)
        {
            Task t = Task.Run(() => MessageEntryEnclave.AddLogEntryAsync(message,header,method,methodvalues));
            RemoveCurrentTask(t);
        }


        /// <summary>
        /// Add debug log info to logging file. Even if called and values are passed in code, this method can be disabled at runtime or in deployment envionment by adding Appconfig-> Appsettings-> 'DebugLogEnabled' entry. Note that this is an Asynchronous operation.
        /// Refer to WaitForLogComplete() is you have too much of data to be written or application lifetime issues on logging.
        /// </summary>
        /// <param name="message">Message to log into the file</param>
        /// <param name="header">Header information or any additional information which can add to exception troubleshooting.</param>
        /// <param name="method">Get an instance of current Method. Use 'MethodBase.GetCurrentMethod()' to get the current method. If value is not passed or null methodvalues will be ignored.</param>
        /// <param name="methodvalues">Values that are passed into current method which try catch is written/caught. The param array must be in the same order of method parameters. If exception happens in a child method then these values can be picked from logs and used as a starting point of debug.</param>
        public static void AddDebugEntry(string message, string header = "", MethodBase method = null, params object[] methodvalues)
        {
            if (_DebugLogEnabled == true)
            {
                Task t = Task.Run(() =>   MessageEntryEnclave.AddDebugEntryAsync(message, header,method,methodvalues));
                RemoveCurrentTask(t);
            }
        }

        /// <summary>
        /// Get foramtted exception entries to write into an alternative logging source like DB. Note that this is a Synchronous operation.
        /// </summary>
        /// <param name="ex">Exception object</param>
        /// <param name="header">Header information or any additional information which can add to exception troubleshooting.</param>
        /// <param name="methodvalues">Values that are passed into current method which try catch is written/caught. The param array must be in the same order of method parameters. If exception happens in a child method then these values can be picked from logs and used as a starting point of debug.</param>
        public static string GetExceptionString(Exception ex, string header = "", params object[] methodvalues)
        {
            return  MessageEntryEnclave.GetExceptionEntry(ex, header,methodvalues); 
        }

        /// <summary>
        /// Get foramtted log entries to write into an alternative logging source like DB. Note that this is a Synchronous operation.
        /// </summary>
        /// <param name="ex">Exception object</param>
        /// <param name="header">Header information or any additional information which can add to exception troubleshooting.</param>
        /// <param name="method">Get an instance of current Method. Use 'MethodBase.GetCurrentMethod()' to get the current method. If value is not passed or null methodvalues will be ignored.</param>
        /// <param name="methodvalues">Values that are passed into current method which try catch is written/caught. The param array must be in the same order of method parameters. If exception happens in a child method then these values can be picked from logs and used as a starting point of debug.</param>
        public static string GetLogString(string logentry, string header = "", MethodBase method = null, params object[] methodvalues)
        {
            return  MessageEntryEnclave.GetLogEntry(logentry, header,method,methodvalues); 
        }

        /// <summary>
        /// Writes to OS application log. Note that this is an Asynchronous operation.
        /// Refer to WaitForLogComplete() is you have too much of data to be written or application lifetime issues on logging.
        /// </summary>
        /// <param name="msg">message to be written</param>
        /// <param name="msgtype">type of message</param>
        /// <param name="method">Get an instance of current Method. Use 'MethodBase.GetCurrentMethod()' to get the current method. If value is not passed or null methodvalues will be ignored.</param>
        /// <param name="methodvalues">Values that are passed into current method which try catch is written/caught. The param array must be in the same order of method parameters. If exception happens in a child method then these values can be picked from logs and used as a starting point of debug.</param>
        public static void WriteToOSApplicationLog(string msg, EventLogEntryType msgtype = EventLogEntryType.Error, MethodBase method = null, params object[] methodvalues)
        {
            Task t = Task.Run(() =>  MessageEntryEnclave.WriteToOSApplicationLog(msg, msgtype,method,methodvalues));
            RemoveCurrentTask(t);
        }

        /// <summary>
        /// Writes and exception to OS application log. Note that this is a Asynchronous operation.
        /// Refer to WaitForLogComplete() is you have too much of data to be written or application lifetime issues on logging.
        /// </summary>
        /// <param name="ex">Exception which need to be logged.</param>
        /// <param name="msgtype">type of message</param>
        /// <param name="methodvalues">Values that are passed into current method which try catch is written/caught. The param array must be in the same order of method parameters. If exception happens in a child method then these values can be picked from logs and used as a starting point of debug.</param>
        public static void WriteToOSApplicationLog(Exception ex, EventLogEntryType msgtype = EventLogEntryType.Error, params object[] methodvalues)
        {
            Task t = Task.Run(() =>  MessageEntryEnclave.WriteToOSApplicationLog(ex, msgtype, methodvalues));
            RemoveCurrentTask(t);
        }

        /// <summary>
        /// Makes the current thread Wait until any of the pending messages/Exceptions/Logs are completly written into respective sources.
        /// Call this method before application is shutdown to make sure all logs are saved properly.
        /// </summary>
        public static void WaitForLogComplete()
        {
            Task.WaitAll(tasks.Values.ToArray());
        }

        static void RemoveCurrentTask(Task t)
        {
            tasks.GetOrAdd(t.Id, t);
            t.ContinueWith((x) =>
            {
                bool removed = tasks.TryRemove(t.Id, out t);
                while (removed == false) //in rare cases where there is a huge amount of exceptions are logged the collection can go busy and this returns false. so retry that time.
                {
                    removed = tasks.TryRemove(t.Id, out t);
                    t.Wait(1000);
                }
            });
        }

        /// <summary>
        /// since Tasks are handled in a return void way and exceptions cannot be caught by endusers if an issue occures, 
        /// all methods in this class are strictly code walked to have try catch and exception caught and logged. 
        /// Only case as of now I see is exception on WriteToOSApplicationLog where no option is left.
        /// </summary>
        private class MessageEntryEnclave
        {
            public static bool WriteToOSApplicationLog(string msg, EventLogEntryType msgtype = EventLogEntryType.Error)
            {
                try //create a section if the complete section doesn't exist
                {
                    if (EventLog.SourceExists(_WindowsEventsSectionName) == false)
                        EventLog.CreateEventSource(_WindowsEventsSectionName, "Application");
                    EventLog.WriteEntry(_WindowsEventsSectionName, msg, msgtype);
                    return true;
                }
                catch (System.Security.SecurityException ex)
                {
                    if (ex.Message == "Requested registry access is not allowed.") //app do not have permission. so use this as a fail safe. As of windows 7 there is a windows source called "Application Error".
                    {
                        EventLog.WriteEntry("Application Error", msg, msgtype);
                        _WindowsEventsSectionName = "Application Error";
                    }
                    return true;
                }
                catch
                {
                    //Since OS log also failed, no logs will be left. Only thing user will get is a console message, if application is running as a console.
                    Console.Write(msg);
                    return false;
                }
            }

            public static bool WriteToOSApplicationLog(string message, EventLogEntryType msgtype = EventLogEntryType.Error, MethodBase method = null, params object[] methodvalues)
            {
                string msg = "";
                if (method != null)
                {
                    msg = GetMethodParameters(method, methodvalues);
                }
                msg = message + " - " + msg;
                return MessageEntryEnclave.WriteToOSApplicationLog(msg, msgtype);
            }

            public static bool WriteToOSApplicationLog(Exception ex, EventLogEntryType msgtype = EventLogEntryType.Error, params object[] methodvalues)
            {
                return MessageEntryEnclave.WriteToOSApplicationLog(MessageEntryEnclave.CreateMessageLogEntry(ex,methodvalues), msgtype);
            }

            public static string CreateMessageLogEntry(Exception ex, params object[] values)
            {
                string msg = "";
                try
                {
                    Exception inex = ex;
                    msg = ex.Message;
                    string OrignMethod = "";
                    StackTrace stackTrace = new StackTrace(ex);
                    if (stackTrace.GetFrames() != null && stackTrace.GetFrame(stackTrace.GetFrames().Length -1) != null)
                    {
                        MethodInfo info = (MethodInfo)stackTrace.GetFrame(stackTrace.GetFrames().Length - 1).GetMethod();
                        OrignMethod = "METHOD EXCEPTION CAUGHT = " + info.ReflectedType.Namespace + "." + info.ReflectedType.Name +
                            "." + GetMethodParameters(info, values);
                    }
                    string msgheader = ". METHOD EXCEPTION TRIGGERED = ";
                    #if !DEBUG
                        msgheader = ". METHOD EXCEPTION TRIGGERED (WARNING: RELEASE MODE - CALLSTACK INFO MIGHT BE INCORRECT. ACTUAL ERROR COULD BE SOMEWHERE ABOVE THE STACK). = ";
                    #endif
                    if (stackTrace.GetFrame(0) != null)
                    {

                        MethodInfo info = (MethodInfo)stackTrace.GetFrame(stackTrace.GetFrames().Length - 1).GetMethod();
                        OrignMethod += msgheader  + info.ReflectedType.Namespace + "." + info.ReflectedType.Name + "." + stackTrace.GetFrame(0).GetMethod().Name;
                        OrignMethod += "(";
                        foreach (var item in stackTrace.GetFrame(0).GetMethod().GetParameters())
                        {
                            OrignMethod += item.ToString() + ", ";
                        }
                        if (OrignMethod.EndsWith("(") == false)
                            OrignMethod = OrignMethod.Substring(0, OrignMethod.Length - 2);
                        OrignMethod += ")";
                    }
                    while (inex.InnerException != null)
                    {
                        if (inex == null)
                            break;
                        inex = ex.InnerException;
                        msg = msg + " -- INNER EXCEPTION --" + inex.Message;
                    };
                    msg = msg + " - " + OrignMethod + ". STACK TRACE: -- " + ex.StackTrace + ".";
                    return msg;
                }
                catch(Exception e)
                {
                    return "Unexpected error during CreateMessageLogEntry: " + e.Message + ". Original Exception message (Partial):" + msg;
                }
            }

            private static string GetMethodParameters(MethodBase method, params object[] values)
            {
                string msg = "";
                try
                {
                    ParameterInfo[] parms = method.GetParameters();
                    object[] namevalues = new object[2 * parms.Length];
                    msg = method.Name + "(";
                    for (int i = 0, j = 0; i < parms.Length; i++, j += 2)
                    {
                        msg += " {" + j + "}={" + (j + 1) + "},";
                        namevalues[j] = parms[i].ToString();
                        if (i < values.Length)
                            namevalues[j + 1] = values[i];
                        else
                            namevalues[j + 1] = "?";
                    }
                    msg = msg.TrimEnd(new char[] { ',' });
                    msg += ")";
                    return string.Format(msg, namevalues);
                }
                catch (Exception ex)
                {
                    return "Unexpected error during GetMethodParameters: " + ex.Message + ". Original Exception message (Partial):" + msg;
                }
            }

            static void ProcessMessage(string filepath, string message)
            {
                AppendText(filepath, Environment.NewLine + DateTime.Now.ToString() + ": " + message);
            }

            public static void AddExceptionEntryAsync(Exception ex, string header, params object[] values)
            {
                string msg = CreateMessageLogEntry(ex,values);
                ProcessMessage(_ExceptionFilePath, header + " - " + msg);
            }

            public static void AddLogEntryAsync(string message, string header = "", MethodBase method = null, params object[] values)
            {
                if (header != "")
                    message = header + " - " + message;
                if (method != null)
                {
                    message += " - Origin Method: " + GetMethodParameters(method, values);
                }
                ProcessMessage(_LogFilepath, message);
            }

            public static void AddLogEntryAsync1(string message, string header = "", MethodBase method = null, params object[] values)
            {
                if (header != "")
                    message = header + " - " + message;
                if (method != null)
                {
                    message += " - Origin Method: " + GetMethodParameters(method, values);
                }
                //ProcessMessage(logEntrypath, message);
            }
            public static void AddDebugEntryAsync(string message, string header = "", MethodBase method = null, params object[] values)
            {
                string msg = "";
                if (header != "")
                    msg = header + " - " + message;
                if (method != null)
                {
                    msg += " - Origin Method: " + GetMethodParameters(method, values); 
                }
                ProcessMessage(_DebugFilePath, msg);
            }

            public static string GetExceptionEntry(Exception ex, string header, params object[] values)
            {
                string msg = CreateMessageLogEntry(ex,values);
                header = DateTime.Now.ToString() + ": " + header + " - " + msg;
                return header;
            }

            public static string GetLogEntry(string logentry, string header, MethodBase method = null, params object[] values)
            {
                string msg = "";
                if (header != "")
                    msg = DateTime.Now.ToString() + ": " + header + " - " + logentry;
                if (method != null)
                {
                    msg += " - Origin Method: " + GetMethodParameters(method, values);
                }
                return msg;
            }

            static readonly object locker = new object();
            public static void AppendText(string path, string content)
            {
                try
                {
                    lock (locker)
                    {
                        //this file method writes all text you pass     
                        File.AppendAllText(path, content);
                    }
                }
                catch (System.IO.IOException)
                {
                    AppendTextFailSafe(path, content);
                }
                catch (Exception ex)
                {
                    //incase any other exception so that no file can be written then log to application log of the system.
                    WriteToOSApplicationLog(ex, System.Diagnostics.EventLogEntryType.Error);
                }
            }

            public static void AppendTextFailSafe(string path, string content)
            {
                try
                {
                    bool CompleteFlag = false;
                    int i = 0; //2 min (default) time to write. or release this thread.
                    while (CompleteFlag == false || i < (2 * _FileWriteWaitTimeoutSeconds)) //multiply by 2 here because sleep is for 1/2 second and timer adds every second twice. so count is almost twice 60 seconds = 120
                    {
                        try
                        {
                            File.AppendAllText(path, content);
                            CompleteFlag = true;
                            i++;
                            return;
                        }
                        catch
                        {
                            Thread.Sleep(500); //wait for 1/2 sec and then retry until timout is reached.
                        }
                    }
                    if (CompleteFlag == false)
                        WriteToOSApplicationLog("Unable to write to log file: " + path + ", Message: " + content, System.Diagnostics.EventLogEntryType.Error);
                }
                catch (Exception ex)
                {
                    //incase any other exception so tht no file can be written then log to application log of the system
                    WriteToOSApplicationLog(ex, System.Diagnostics.EventLogEntryType.Error);
                }
            }
        }
    }

    public enum DebugLogActionEnum
    {
        FollowConfig,
        Disabled,
        Enabled
    };
}

