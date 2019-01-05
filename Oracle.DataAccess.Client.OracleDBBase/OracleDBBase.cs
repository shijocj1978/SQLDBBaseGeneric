using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Configuration;
using System.Threading.Tasks;
using Oracle.DataAccess.Client;
using System.Data;
using DBGeneric;
using System.Data.Common;
using System.Reflection;
using System.Globalization;

namespace Oracle.DataAccess.Client
{
     /// <summary>
    /// Provides an abstract and easy way for accessing Oracle database with built-in Exception handling.
    /// <para>----------------------------------------------------------------------------------------------------------------------------------------------------</para>
    /// <para>    DEFAULT VALUES WHICH CAN BE MODIFIED BY ADDING ENTRIES TO APP.CONFIG-> APPSETTINGS SECTION</para>
    /// <para>----------------------------------------------------------------------------------------------------------------------------------------------------</para>
    /// <para> OracleBulkCopyWriteThreshold: Default value = [10000] Allowed values:  0 TO Int.MaxValue</para>
    /// <para> OracleExceptionActionModeEnum: Default value = [ExceptionActionModeEnum.LogAndRethrow] Allowed values:  Rethrow, Log, LogAndRethrow .</para>
    /// <para> OracleDBTimeoutRetryCount: Default value = 3.  Allowed values. 0 to DBTimeoutRetryCount * RetryMultiplyValue. Must be less than int.Maxvalue. Retires upto specified times at a rate of increasing interval at each failure.</para>
    /// <para> OracleRetryMultiplyValue: Default value = [10000]. Allowed values. Increment of wait time multiplied by this factor for each retry. Wait time is calculated as DBTimeoutRetryCount * RetryMultiplyValue = eg: 1/2/3/4/5 * 1000 = 1,2,3,4,5 sec or 1/2/3/4/5 * 10000 = 10/20/30/40/50 sec etc.</para>
    /// </summary>
    public class OracleDBBase : DBBase
    {
        #region Basics

        public OracleDBBase(): base()
        {
            if (ConfigurationManager.AppSettings.AllKeys.Contains("OracleRetryMultiplyValue") == true)
            {
                if (int.TryParse(ConfigurationManager.AppSettings["OracleRetryMultiplyValue"].ToString(), out _RetryMultiplyValue) == false)
                    throw new ArgumentException("Error while parsing configuration value Appsettings->OracleRetryMultiplyValue. Please check the value.");
                if (_RetryMultiplyValue <= 0)
                    throw new ArgumentException("Error while parsing configuration value Appsettings->OracleRetryMultiplyValue. The value must be > 1 and must be calculated in milliseconds.");
            }
            else
                _RetryMultiplyValue = 10000;

            if (ConfigurationManager.AppSettings.AllKeys.Contains("OracleDBTimeoutRetryCount") == true)
            {
                if (int.TryParse(ConfigurationManager.AppSettings["OracleDBTimeoutRetryCount"].ToString(), out _DBTimeoutRetryCount) == false)
                    throw new ArgumentException("Error while parsing configuration value Appsettings->OracleDBTimeoutRetryCount. Please check the value.");
                if (_DBTimeoutRetryCount <= 0 || (_DBTimeoutRetryCount * _RetryMultiplyValue) > int.MaxValue)
                    throw new ArgumentException("Error while parsing configuration value Appsettings->OracleDBTimeoutRetryCount. The value must between 1 and " + (int.MaxValue / _RetryMultiplyValue).ToString());
            }
            else
                _DBTimeoutRetryCount = 3;

            if (ConfigurationManager.AppSettings.AllKeys.Contains("OracleExceptionActionModeEnum") == true)
            {
                if (Enum.TryParse<ExceptionActionModeEnum>(ConfigurationManager.AppSettings["OracleExceptionActionModeEnum"].ToString(), true, out _exceptionActionMode) == false)
                    throw new ArgumentException("Error while parsing configuration value Appsettings->OracleExceptionActionModeEnum. Allowed values are  Rethrow, Log & LogAndRethrow. Please check the value.");
            }
            else
                ExceptionActionMode = ExceptionActionModeEnum.LogAndRethrow;
        }

        #endregion

        #region ExecuteSelect

        /// <summary>
        /// Executes a select and returns one parameter.
        /// </summary>
        /// <typeparam name="T">Type of object to return. Object passed must contain a Typecast overload.</typeparam>
        /// <param name="connection">Connection to DB</param>
        /// <param name="sql">Sql query</param>
        /// <param name="returnParamName">Feild name of the column which the value need to be returned.</param>
        /// <param name="onException">Reference to method which can add additional information to logs incase of an exception. By default values of all paramaters (basic datatypes) from command will be logged to the exception.</param>
        /// <returns>Value of the feild name specifed in the Type specified.</returns>
        public  override T ExecuteScalarQuery<T>(DbConnection connection, string sql, string returnParamName, Func<DbCommand, Exception, string> onException = null) //where T : new()
        {
            int retrycnt = 0, maxretry = _DBTimeoutRetryCount;
            DateTime starttime = DateTime.Now;OracleCommand cmd = null;
            T t = default(T);
            while (retrycnt < maxretry)
            {
                try
                {
                    starttime = DateTime.Now;
                    cmd = ((OracleConnection)connection).CreateCommand();
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandText = sql;
                    cmd.CommandTimeout = connection.ConnectionTimeout;
                    if (Logging.DebugLogEnabled == true)
                        Logging.AddDebugEntry(GetAllUsedCommandParametersAsString(cmd), "ExecuteScalarQuery command paramaters:", MethodBase.GetCurrentMethod());
                    connection.Open();
                    OracleDataReader rdr = cmd.ExecuteReader();
                    t = GetScalarValue<T>(rdr, returnParamName);
                    retrycnt = maxretry;
                    rdr.Close();
                    break;
                }
                catch (Exception ex)
                {
                    HandleExceptionRetry(ex, ref retrycnt, ref maxretry, starttime,cmd,onException);
                }
                finally
                {
                    if (connection.State == ConnectionState.Open)
                        connection.Close();
                }
            }
            return t;
        }

        /// <summary>
        /// Executes a select and returns one parameter.
        /// </summary>
        /// <typeparam name="T">Type of object to return. Object passed must contain a Typecast overload.</typeparam>
        /// <param name="connectionStr">Connection string to DB</param>
        /// <param name="sql">Sql query</param>
        /// <param name="returnParamName">Feild name of the column which the value need to be returned.</param>
        /// <param name="customTimeoutValue">Optional. Use it to increase wait times incase the DB is using longrunning operations.</param>
        /// <param name="onException">Reference to method which can add additional information to logs incase of an exception. By default values of all paramaters (basic datatypes) from command will be logged to the exception.</param>
        /// <param name="doSimpleTransaction">Performs a simple atomic transaction which is limited to this DB call. This provides an easy way to manage transactions. If you want to do advanced transaction operations then create an external connection and pass that to method instead.</param>
        /// <returns>Value of the feild name specifed in the Type specified.</returns>
        public  override T ExecuteScalarQuery<T>(string connectionStr, string sql, string returnParamName, Func<DbCommand, Exception, string> onException = null, int customTimeoutValue = -1) //where T : new()
        {
            OracleConnection con = (OracleConnection)CreateConnection(connectionStr,customTimeoutValue);
            return ExecuteScalarQuery<T>(con, sql, returnParamName, onException);
        }

        /// <summary>
        /// Executes the query and returns the total number of rows affected by the command. If more than one query is passed then the return value contains total number of affected rows by all queries.
        /// </summary>
        /// <param name="connection">Connection to DB</param>
        /// <param name="sql">Sql query</param>
        /// <param name="onException">Reference to method which can add additional information to logs incase of an exception. By default values of all paramaters (basic datatypes) from command will be logged to the exception.</param>
        /// <param name="doSimpleTransaction">Performs a simple atomic transaction which is limited to this DB call. This provides an easy way to manage transactions. If you want to do advanced transaction operations then create an external connection and pass that to method instead.</param>
        /// <returns>Number of rows affected</returns>
        public  override int ExecuteBasicQuery(DbConnection connection, string sql, Func<DbCommand, Exception, string> onException = null)
        {
            int retrycnt = 0, maxretry = _DBTimeoutRetryCount;
            DateTime starttime = DateTime.Now; OracleCommand cmd = null;
            int returnvalue = -1;
            while (retrycnt < maxretry)
            {
                try
                {
                    starttime = DateTime.Now;
                     cmd = ((OracleConnection)connection).CreateCommand();
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandText = sql;
                    cmd.CommandTimeout = connection.ConnectionTimeout;
                    if (Logging.DebugLogEnabled == true)
                        Logging.AddDebugEntry(GetAllUsedCommandParametersAsString(cmd), "ExecuteBasicQuery command paramaters:", MethodBase.GetCurrentMethod());
                    connection.Open();
                    returnvalue = cmd.ExecuteNonQuery();
                    retrycnt = maxretry;
                    break;
                }
                catch (Exception ex)
                {
                    HandleExceptionRetry(ex, ref retrycnt, ref maxretry, starttime,cmd,onException);
                }
                finally
                {
                    if (connection.State == ConnectionState.Open)
                        connection.Close();
                }
            }
            return returnvalue;
        }

        /// <summary>
        /// Executes the query and returns the total number of rows affected by the command. If more than one query is passed then the return value contains total number of affected rows by all queries.
        /// </summary>
        /// <param name="connectionStr">Connection string to DB</param>
        /// <param name="sql">Sql query</param>
        /// <param name="doSimpleTransaction">Performs a simple atomic transaction which is limited to this DB call. This provides an easy way to manage transactions. If you want to do advanced transaction operations then create an external connection and pass that to method instead.</param>
        /// <param name="onException">Reference to method which can add additional information to logs incase of an exception. By default values of all paramaters (basic datatypes) from command will be logged to the exception.</param>
        /// <param name="customTimeoutValue">Optional. Use it to increase wait times incase the DB is using longrunning operations.</param>
        /// <returns>Number of rows affected</returns>
        public  override int ExecuteBasicQuery(string connectionStr, string sql, Func<DbCommand, Exception, string> onException = null, int customTimeoutValue = -1)
        {
            OracleConnection con = (OracleConnection)CreateConnection(connectionStr, customTimeoutValue);
            return ExecuteBasicQuery(con, sql, onException);
        }

        /// <summary>
        /// Executes a select and returns object in the specified type. transform logic paramater should be used to convert the reader object into specified object.
        /// </summary>
        /// <typeparam name="T">Type of return paramater.</typeparam>
        /// <param name="connection">Connection to DB</param>
        /// <param name="sql">Sql query</param>
        /// <param name="transformlogic">Reference to method which performs the transformation of the reader object to custom object.</param>
        /// <param name="onException">Reference to method which can add additional information to logs incase of an exception. By default values of all paramaters (basic datatypes) from command will be logged to the exception.</param>
        /// <param name="doSimpleTransaction">Performs a simple atomic transaction which is limited to this DB call. This provides an easy way to manage transactions. If you want to do advanced transaction operations then create an external connection and pass that to method instead.</param>
        /// <returns>Value of the feild name specifed in the Type specified.</returns>
        public  override T ExecuteComplexQuery<T>(DbConnection connection, string sql, Func<DbDataReader,  T> transformlogic, Func<DbCommand, DbDataReader, Exception, string> onException = null) //where T : new()
        {
            int retrycnt = 0, maxretry = _DBTimeoutRetryCount;
            DateTime starttime = DateTime.Now; OracleCommand cmd = null; OracleDataReader rdr = null;
            T t = default(T);
            while (retrycnt < maxretry)
            {
                try
                {
                    starttime = DateTime.Now;
                    cmd = ((OracleConnection)connection).CreateCommand();
                    cmd.CommandType = System.Data.CommandType.Text;
                    cmd.CommandText = sql;
                    cmd.CommandTimeout = connection.ConnectionTimeout;
                    if (Logging.DebugLogEnabled == true)
                        Logging.AddDebugEntry(GetAllUsedCommandParametersAsString(cmd), "ExecuteComplexQuery command paramaters:", MethodBase.GetCurrentMethod());
                    connection.Open();
                    rdr = cmd.ExecuteReader();
                    t = transformlogic(rdr);
                    retrycnt = maxretry;
                    rdr.Close();
                    break;
                }
                catch (Exception ex)
                {
                    HandleExceptionRetry(ex, ref retrycnt, ref maxretry, starttime,cmd,rdr,onException);
                }
                finally
                {
                    if (connection.State == ConnectionState.Open)
                        connection.Close();
                }
            }
            return t;
        }

        /// <summary>
        /// Executes a select and returns object in the specified type. transform logic paramater should be used to convert the reader object into specified object.
        /// </summary>
        /// <typeparam name="T">Type of return paramater.</typeparam>
        /// <param name="connectionStr">Connection string to DB</param>
        /// <param name="sql">Sql query</param>
        /// <param name="transformlogic">Reference to method which performs the transformation of the reader object to custom object.</param>
        /// <param name="onException">Reference to method which can add additional information to logs incase of an exception. By default values of all paramaters (basic datatypes) from command will be logged to the exception.</param>
        /// <param name="doSimpleTransaction">Performs a simple atomic transaction which is limited to this DB call. This provides an easy way to manage transactions. If you want to do advanced transaction operations then create an external connection and pass that to method instead.</param>
        /// <param name="customTimeoutValue">Optional. Use it to increase wait times incase the DB is using longrunning operations.</param>
        /// <returns>Value of the feild name specifed in the Type specified.</returns>
        public override T ExecuteComplexQuery<T>(string connectionStr, string sql, Func<DbDataReader, T> transformlogic, Func<DbCommand, DbDataReader, Exception, string> onException = null, int customTimeoutValue = -1) //where T : new()
        {
            OracleConnection con = (OracleConnection)CreateConnection(connectionStr, customTimeoutValue);
            return ExecuteComplexQuery<T>(con, sql, transformlogic, onException);
        }

        #endregion

        #region Execute Storedprocedure

        /// <summary>
        /// Executes a procedure and returns the number of rows affected. Use commandparams to assign paramaters.
        /// Use method InsertParams(cmd,x,x,...) to easly assign the values.
        /// NOTE: If the procedure contain an out paramater then use ExecuteComplexStoredProcedure instead.
        /// </summary>
        /// <param name="connection">Connection to DB</param>
        /// <param name="procedureName">Name of procedure.</param>
        /// <param name="commandParams">Paramater Values. Use method InsertParams(cmd,x,x,...) to easly assign the values.</param>
        /// <param name="onException">Reference to method which can add additional information to logs incase of an exception. By default values of all paramaters (basic datatypes) from command will be logged to the exception.</param>
        /// <param name="doSimpleTransaction">Performs a simple atomic transaction which is limited to this DB call. This provides an easy way to manage transactions. If you want to do advanced transaction operations then create an external connection and pass that to method instead.</param>
        /// <returns>total number of rows affected.</returns>
        public  override int ExecuteBasicStoredProcedure(DbConnection connection, string procedureName, Action<DbCommand> commandParams = null, Func<DbCommand, Exception, string> onException = null, bool doSimpleTransaction = false)
        {
            int retrycnt = 0, maxretry = _DBTimeoutRetryCount;
            DateTime starttime = DateTime.Now; OracleCommand cmd = null;
            int ret = -1;
            while (retrycnt < maxretry)
            {
                try
                {
                    starttime = DateTime.Now;
                    cmd = ((OracleConnection)connection).CreateCommand();
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    if (commandParams != null)
                        commandParams(cmd);
                    cmd.CommandText = procedureName;
                    cmd.CommandTimeout = connection.ConnectionTimeout;
                    connection.Open();
                    if (Logging.DebugLogEnabled == true)
                        Logging.AddDebugEntry(GetAllUsedCommandParametersAsString(cmd), "ExecuteBasicStoredProcedure command paramaters:", MethodBase.GetCurrentMethod());
                    if (doSimpleTransaction == true)
                        cmd.Transaction = ((OracleConnection)connection).BeginTransaction();
                    ret = cmd.ExecuteNonQuery();
                    retrycnt = maxretry;
                    if (doSimpleTransaction == true)
                        cmd.Transaction.Commit();
                    break;
                }
                catch (Exception ex)
                {
                    //note: upon exception transaction object is cleared and never will be there a transaction.commit(). So it is an implicit rollback.
                    HandleExceptionRetry(ex, ref retrycnt, ref maxretry, starttime, cmd, onException);
                }
                finally
                {
                    if (connection.State == ConnectionState.Open)
                        connection.Close();
                }
            }
            return ret;
        }

        /// <summary>
        /// Executes a procedure and returns the number of rows affected. Use commandparams to assign paramaters.
        /// Use method InsertParams(cmd,x,x,...) to easly assign the values.
        /// NOTE: If the procedure returns a value as return instead of out value then use 'InsertReturnValue' method to assign a return value and use that value instead.
        /// </summary>
        /// <param name="connectionStr">Connection string to DB</param>
        /// <param name="procedureName">Name of procedure.</param>
        /// <param name="commandParams">Paramater Values. Use method InsertParams(cmd,x,x,...) to easly assign the values.</param>
        /// <param name="onException">Reference to method which can add additional information to logs incase of an exception. By default values of all paramaters (basic datatypes) from command will be logged to the exception.</param>
        /// <param name="doSimpleTransaction">Performs a simple atomic transaction which is limited to this DB call. This provides an easy way to manage transactions. If you want to do advanced transaction operations then create an external connection and pass that to method instead.</param>
        /// <param name="customTimeoutValue">Optional. Use it to increase wait times incase the DB is using longrunning operations.</param>
        /// <returns>total number of rows affected.</returns>
        public override int ExecuteBasicStoredProcedure(string connectionStr, string procedureName, Action<DbCommand> commandParams = null, Func<DbCommand, Exception, string> onException = null, bool doSimpleTransaction = false, int customTimeoutValue = -1)
        {
            OracleConnection con = (OracleConnection)CreateConnection(connectionStr, customTimeoutValue);
            return ExecuteBasicStoredProcedure(con, procedureName, commandParams, onException, doSimpleTransaction);
        }

        /// <summary>
        /// Executes a function or a procedure with only one out parameter and returns the value of the return paramater name as the type specified.
        /// NOTE: If the procedure have more than one out parameter or if the function is having an out parameter then this method will fail and so use 'InsertReturnValue' method to assign a return value and use that value instead.
        /// </summary>
        /// <typeparam name="T">Type of paramater to be returned.</typeparam>
        /// <param name="connection">Connection to DB</param>
        /// <param name="procedureName">Name of procedure.</param>
        /// <param name="outParamDbType">DB Type of outparamater. It should be enum of type OracleDbType</param>
        /// <param name="outParamSize">Size of out paramater if required. if not required then give 0</param>
        /// <param name="outParamName">Name of parameter which the value need to be returned. This need to be null if the procedureName is a function</param>
        /// <param name="commandParams">Paramater Values. Use method InsertParams(cmd,x,x,...) to easly assign the values.</param>
        /// <param name="onException">Reference to method which can add additional information to logs incase of an exception. By default values of all paramaters (basic datatypes) from command will be logged to the exception.</param>
        /// <param name="doSimpleTransaction">Performs a simple atomic transaction which is limited to this DB call. This provides an easy way to manage transactions. If you want to do advanced transaction operations then create an external connection and pass that to method instead.</param>        
        /// <returns>Value of the feild name specifed in the Type specified.</returns>
        public override T ExecuteScalarStoredProcedure<T>(DbConnection connection, string procedureName, int outParamSize, Enum outParamDbType, string outParamName = null, Action<DbCommand> commandParams = null, Func<DbCommand, Exception, string> onException = null, bool doSimpleTransaction = false) //where T : new()
        {
            int retrycnt = 0, maxretry = _DBTimeoutRetryCount;
            DateTime starttime = DateTime.Now; OracleCommand cmd = null;
            T t = default(T);
            while (retrycnt < maxretry)
            {
                try
                {
                    OracleParameter retparam = null;
                    if (outParamDbType.GetType() != typeof(OracleDbType))
                        throw new ArgumentException("Invalid Enumeration Type detected. Only enums of type 'OracleDbType' are allowed in outParamDbType.");
                    starttime = DateTime.Now;
                    cmd = ((OracleConnection)connection).CreateCommand();
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.CommandText = procedureName;
                    commandParams(cmd);
                    if (outParamName == null || outParamName == "")
                    {
                        retparam = InsertReturnParameter<OracleParameter>(cmd, outParamSize);
                        retparam.OracleDbType = (OracleDbType)outParamDbType; //done here since it is changing for each provider.
                    }
                    else
                        InsertOutParameter(cmd, outParamName, paramSize: outParamSize);
                    cmd.CommandTimeout = connection.ConnectionTimeout;
                    if (Logging.DebugLogEnabled == true)
                        Logging.AddDebugEntry(GetAllUsedCommandParametersAsString(cmd), "ExecuteScalarStoredProcedure command paramaters:", MethodBase.GetCurrentMethod());
                    connection.Open();
                    //OracleCommandBuilder.DeriveParameters(cmd);
                    if (doSimpleTransaction == true)
                        cmd.Transaction = ((OracleConnection)connection).BeginTransaction();
                    cmd.ExecuteNonQuery();
                    if (retparam == null)
                        t = GetScalarValue<T>(cmd, outParamName);
                    else
                        t = GetScalarValue<T>(retparam);
                    retrycnt = maxretry;
                    if (doSimpleTransaction == true)
                        cmd.Transaction.Commit();
                    break;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Contains("is not a procedure or is undefined"))
                        ex = new Exception("Possible issue with SPORC/Function signature. Additional Info: If you are calling a function. You may have specified an out paramater as in 'outParamName' for a function. This will not work.", ex);
                    HandleExceptionRetry(ex, ref retrycnt, ref maxretry, starttime,cmd,onException);
                }
                finally
                {
                    if (connection.State == ConnectionState.Open)
                        connection.Close();
                }
            }
            return t;
        }

        /// <summary>
        /// Executes a procedure and returns the value of the return paramater name as the type specified.
        /// NOTE: If the procedure returns a value as return instead of out value then use 'InsertReturnValue' method to assign a return value and use that value instead.
        /// </summary>
        /// <typeparam name="T">Type of paramater to be returned.</typeparam>
        /// <param name="connectionStr">Connection string to DB</param>
        /// <param name="procedureName">Name of procedure.</param>
        /// <param name="outParamName">Name of parameter which the value need to be returned.</param>
        /// <param name="outParamDbType">DB Type of outparamater. It should be enum of type OracleDbType</param>
        /// <param name="outParamSize">Size of out paramater if required. if not required then give 0</param>
        /// <param name="outParamSize">Size of out paramater object</param>
        /// <param name="commandParams">Paramater Values. Use method InsertParams(cmd,x,x,...) to easly assign the values.</param>
        /// <param name="onException">Reference to method which can add additional information to logs incase of an exception. By default values of all paramaters (basic datatypes) from command will be logged to the exception.</param>
        /// <param name="doSimpleTransaction">Performs a simple atomic transaction which is limited to this DB call. This provides an easy way to manage transactions. If you want to do advanced transaction operations then create an external connection and pass that to method instead.</param>
        /// <param name="customTimeoutValue">Optional. Use it to increase wait times incase the DB is using longrunning operations.</param>
        /// <returns>Value of the feild name specifed in the Type specified.</returns>
        public override T ExecuteScalarStoredProcedure<T>(string connectionStr, string procedureName, int outParamSize, Enum outParamDbType, string outParamName = null, Action<DbCommand> commandParams = null, Func<DbCommand, Exception, string> onException = null, bool doSimpleTransaction = false, int customTimeoutValue = -1)// where T : new()
        {
            OracleConnection con = (OracleConnection)CreateConnection(connectionStr, customTimeoutValue);
            return ExecuteScalarStoredProcedure<T>(con, procedureName, outParamSize, outParamDbType, outParamName,commandParams,onException, doSimpleTransaction);
        }

        /// <summary>
        /// Executes a procedure and returns the dataset as the type specified. Use transformlogic to convert SQLDatareader to custom object and commandparams to assign paramaters. 
        /// Use method InsertParams(cmd,x,x,...) to easly assign the values.
        /// NOTE: If the procedure returns a value as return instead of out value then use 'InsertReturnValue' method to assign a return value and use that value instead.
        /// </summary>
        /// <typeparam name="T">Type of paramater to be returned.</typeparam>
        /// <param name="connection">Connection to DB</param>
        /// <param name="procedureName">Name of procedure.</param>
        /// <param name="transformLogic">Reference to method which performs the transformation of the reader object to custom object.</param>
        /// <param name="commandParams">Paramater Values. Use method InsertParams(cmd,x,x,...) to easly assign the values.</param>
        /// <param name="onException">Reference to method which can add additional information to logs incase of an exception. By default values of all paramaters (basic datatypes) from command will be logged to the exception.</param>
        /// <param name="doSimpleTransaction">Performs a simple atomic transaction which is limited to this DB call. This provides an easy way to manage transactions. If you want to do advanced transaction operations then create an external connection and pass that to method instead.</param>
        /// <returns>Value of the feild name specifed in the Type specified.</returns>
        public override T ExecuteComplexStoredProcedure<T>(DbConnection connection, string procedureName, Func<DbDataReader, DbCommand, T> transformLogic, Action<DbCommand> commandParams = null, Func<DbCommand, DbDataReader, Exception, string> onException = null, bool doSimpleTransaction = false)// where T : new()
        {
            int retrycnt = 0, maxretry = _DBTimeoutRetryCount;
            DateTime starttime = DateTime.Now; OracleCommand cmd = null; OracleDataReader rdr = null;
            T t = default(T);
            while (retrycnt < maxretry)
            {
                try
                {
                    starttime = DateTime.Now;
                    cmd = ((OracleConnection)connection).CreateCommand();
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    if (commandParams != null)
                        commandParams(cmd);
                    cmd.CommandText = procedureName;
                    cmd.CommandTimeout = connection.ConnectionTimeout;
                    if (Logging.DebugLogEnabled == true)
                        Logging.AddDebugEntry(GetAllUsedCommandParametersAsString(cmd), "ExecuteComplexStoredProcedure command paramaters:", MethodBase.GetCurrentMethod());
                    connection.Open();
                    if (doSimpleTransaction == true)
                        cmd.Transaction = ((OracleConnection)connection).BeginTransaction();
                    rdr = cmd.ExecuteReader();
                    t = transformLogic(rdr,cmd);
                    retrycnt = maxretry;
                    rdr.Close();
                    if (doSimpleTransaction == true)
                        cmd.Transaction.Commit();
                    break;
                }
                catch (Exception ex)
                {
                    HandleExceptionRetry(ex, ref retrycnt, ref maxretry, starttime,cmd,rdr,onException);
                }
                finally
                {
                    if (connection.State == ConnectionState.Open)
                        connection.Close();
                }
            }
            return t;
        }

        /// <summary>
        /// Executes a procedure and returns the dataset as the type specified. Use transformlogic to convert SQLDatareader to custom object and commandparams to assign paramaters. 
        /// Use method InsertParams(cmd,x,x,...) to easly assign the values.
        /// NOTE: If the procedure returns a value as return instead of out value then use 'InsertReturnValue' method to assign a return value and use that value instead.
        /// </summary>
        /// <typeparam name="T">Type of paramater to be returned.</typeparam>
        /// <param name="connectionStr">Connection string to DB</param>
        /// <param name="procedureName">Name of procedure.</param>
        /// <param name="transformLogic">Reference to method which performs the transformation of the reader object to custom object.</param>
        /// <param name="commandParams">Paramater Values. Use method InsertParams(cmd,x,x,...) to easly assign the values.</param>
        /// <param name="customTimeoutValue">Optional. Use it to increase wait times incase the DB is using longrunning operations.</param>
        /// <param name="onException">Reference to method which can add additional information to logs incase of an exception. By default values of all paramaters (basic datatypes) from command will be logged to the exception.</param>
        /// <param name="doSimpleTransaction">Performs a simple atomic transaction which is limited to this DB call. This provides an easy way to manage transactions. If you want to do advanced transaction operations then create an external connection and pass that to method instead.</param>
        /// <returns>Value of the feild name specifed in the Type specified.</returns>
        public override T ExecuteComplexStoredProcedure<T>(string connectionStr, string procedureName, Func<DbDataReader, DbCommand, T> transformLogic, Action<DbCommand> commandParams = null, Func<DbCommand, DbDataReader, Exception, string> onException = null, bool doSimpleTransaction = false, int customTimeoutValue = -1)// where T : new()
        {
            OracleConnection con = (OracleConnection)CreateConnection(connectionStr, customTimeoutValue);
            return ExecuteComplexStoredProcedure<T>(con, procedureName, transformLogic, commandParams, onException, doSimpleTransaction);
        }

        #endregion

        #region Misc

        protected override DbConnection CreateConnection(string connectionStr, int customTimeoutValue = -1)
        {
            OracleConnection con;
            if (customTimeoutValue != -1)
            {
                OracleConnectionStringBuilder strbld = new OracleConnectionStringBuilder(connectionStr);
                strbld.ConnectionTimeout = customTimeoutValue;
                con = new OracleConnection(strbld.ConnectionString);
            }
            else
                con = new OracleConnection(connectionStr);
            return con;
        }

        // This methods had infinite options and I cannot add all. I did for what I came across. 
        // add your cases below and if it feels that they can be used by others then share and I will add this to library.
        protected override bool UserDefinedConvertionExists<T>(object value, ref T t)
        {
            Type DbType = value.GetType();
            Type OutputType = typeof(T);
            switch (DbType.ToString())
            {
                case "Oracle.DataAccess.Types.OracleString": //case where return/out datatype from DB is varchar2.
                    t = (T)Convert.ChangeType(((Oracle.DataAccess.Types.OracleString)value).Value, typeof(T));
                    break;
                case "System.String": //there are many cases where datatypes are returned as string. cases are below. 
                    {
                        switch (OutputType.ToString())
                        {
                            case "System.DateTime":
                                CultureInfo provider = CultureInfo.InvariantCulture;
                                String format = Consts.ORACLE_TIMESTAMPFORMAT;
                                DateTime date = DateTime.ParseExact(value.ToString(), format, provider);
                                 t = (T)(object)date;
                                break;
                            default:
                                t = (T)value;
                                break;
                        }
                        break;
                    }
                // This methods had infinite options and I cannot add them all. I did for what I came across. 
                // add your cases below and if it feels that they can be used by others then share and I will add this to library.
                default:
                    return false;
            }
            return true;
        }

        protected override string GetAllUsedCommandParametersAsString(DbCommand cmd)
        {
            OracleCommand command = (OracleCommand)cmd;
            if (command == null)
                return "Command object is null.";
            string paramaeters = "";
            try
            {
                if (command.Parameters != null && command.Parameters.Count > 0)
                {
                    foreach (OracleParameter item in command.Parameters)
                    {
                        string param = "[";
                        param += ", ParameterName = " + item.ParameterName;
                        if (item.Value != null)
                        {
                            param += ", Value = " + item.Value.ToString();
                            param += ", DataType = " + item.DbType.ToString();
                            param += ", OracleDataType = " + item.OracleDbType.ToString();
                            param += ", OracleDbTypeEx = " + item.OracleDbTypeEx.ToString();
                        }
                        paramaeters += param + "] ";
                    }
                }
                paramaeters += ", CommandTimeout = " + command.CommandTimeout.ToString();
                paramaeters += ", CommandType = " + command.CommandType.ToString();
                paramaeters += ", CommandText = " + command.CommandText;
                if (command.Connection != null)
                {
                    OracleConnectionStringBuilder strbld = new OracleConnectionStringBuilder(command.Connection.ConnectionString);
                    paramaeters += ", DB Server Info: [ServerName = " + command.Connection.DataSource + "] [DatabaseName = " + command.Connection.Database + "] [User = " + strbld.UserID + "] [Connectiontimeout = " + command.Connection.ConnectionTimeout.ToString();
                }
                return paramaeters;
            }
            catch(Exception ex)
            {
                Logging.AddExceptionEntry(ex, "Error during GetAllUsedCommandParametersAsString()");
                return paramaeters; //return whatever left.
            }
        }

        protected override DbParameter CreatePramaterInstance(string param, int? paramSize = null, Enum paramType = null)
        {
            OracleParameter p = new OracleParameter();
            p.ParameterName = param;
            if (paramType != null)
                p.OracleDbType = p.OracleDbTypeEx = (OracleDbType)paramType;
            if(paramSize != null && paramSize >= 0)
                p.Size = paramSize.Value;
            return p;
        }

        public  void InsertOutParams(DbCommand cmd, 
              string param, OracleDbType paramType, int paramSize, string param1 = "", OracleDbType? paramType1 = null, int? paramSize1 = null, string param2 = "", OracleDbType? paramType2 = null, int? paramSize2 = null, string param3 = "", OracleDbType? paramType3 = null, int? paramSize3 = null
            , string param4 = "", OracleDbType? paramType4 = null, int? paramSize4 = null, string param5 = "", OracleDbType? paramType5 = null, int? paramSize5 = null, string param6 = "", OracleDbType? paramType6 = null, int? paramSize6 = null, string param7 = "", OracleDbType? paramType7 = null, int? paramSize7 = null, string param8 = "", OracleDbType? paramType8 = null, int? paramSize8 = null
            , string param9 = "", OracleDbType? paramType9 = null, int? paramSize9 = null, string param10 = "", OracleDbType? paramType10 = null, int? paramSize10 = null, string param11 = "", OracleDbType? paramType11 = null, int? paramSize11 = null, string param12 = "", OracleDbType? paramType12 = null, int? paramSize12 = null, string param13 = "", OracleDbType? paramType13 = null, int? paramSize13 = null
            , string param14 = "", OracleDbType? paramType14 = null, int? paramSize14 = null, string param15 = "", OracleDbType? paramType15 = null, int? paramSize15 = null)
        {
            try
            {
                if (InsertOutParameter(cmd, param, paramType, paramSize) == false) return;
                if (InsertOutParameter(cmd, param1, paramType1, paramSize1) == false) return;
                if (InsertOutParameter(cmd, param2, paramType2, paramSize2) == false) return;
                if (InsertOutParameter(cmd, param3, paramType3, paramSize3) == false) return;
                if (InsertOutParameter(cmd, param4, paramType4, paramSize4) == false) return;
                if (InsertOutParameter(cmd, param5, paramType5, paramSize5) == false) return;
                if (InsertOutParameter(cmd, param6, paramType6, paramSize6) == false) return;
                if (InsertOutParameter(cmd, param7, paramType7, paramSize7) == false) return;
                if (InsertOutParameter(cmd, param8, paramType8, paramSize8) == false) return;
                if (InsertOutParameter(cmd, param9, paramType9, paramSize9) == false) return;
                if (InsertOutParameter(cmd, param10, paramType10, paramSize10) == false) return;
                if (InsertOutParameter(cmd, param11, paramType11, paramSize11) == false) return;
                if (InsertOutParameter(cmd, param12, paramType12, paramSize12) == false) return;
                if (InsertOutParameter(cmd, param13, paramType13, paramSize13) == false) return;
                if (InsertOutParameter(cmd, param14, paramType14, paramSize14) == false) return;
                if (InsertOutParameter(cmd, param15, paramType15, paramSize15) == false) return;
            }
            catch (Exception ex)
            {
                Logging.AddExceptionEntry(ex, "Invalid data or argument information found in OracleDBBase.InsertOutParams", cmd.CommandText,
                    param, paramType, paramSize, param1, paramType1, paramSize1,param2, paramType2, paramSize2,param3, paramType3, paramSize3,
                    param4, paramType4, paramSize4,param5, paramType5, paramSize5,param6, paramType6, paramSize6,param7, paramType7, paramSize7,
                    param8, paramType8, paramSize8,param9, paramType9, paramSize9,param10, paramType10, paramSize10,param11, paramType11, paramSize11,
                    param12, paramType12, paramSize12,param13, paramType13, paramSize13,param14, paramType14, paramSize14,param15, paramType15, paramSize15);
                throw;
            }
        }

        /// <summary>
        /// Autogenerates DB parameters directly from DB so that the only thing pending for user will be to insert proper values. Note that this will take a round trip to Database and not recommeded in all cases.
        /// <remarks>Note: To use this method the command object passed in must be having open connection, must contain a valid stored procedure or function name with command type as procedure. Also note that any paramaters added already will be removed.</remarks>
        /// </summary>
        /// <param name="cmd">Command object instance</param>
        /// <returns>Status of operation. This will return false if any of the required condition are not met.</returns>
        public override bool AutoGenerateCommandParametersFromDB(DbCommand cmd)
        {
            if (cmd.Connection.State != ConnectionState.Open)
                return false;
            OracleCommandBuilder.DeriveParameters((OracleCommand)cmd);
            return true;
        }

        #endregion

    }


    public class Ora
    {

        static Ora()
        {
            _instance = new OracleDBBase();
        }

        private static OracleDBBase _instance;

        /// <summary>
        /// Provides an abstract and easy way for accessing SQL Server database with built-in Exception handling.
        /// <para>----------------------------------------------------------------------------------------------------------------------------------------------------</para>
        /// <para>    DEFAULT VALUES WHICH CAN BE MODIFIED BY ADDING ENTRIES TO APP.CONFIG-> APPSETTINGS SECTION</para>
        /// <para>----------------------------------------------------------------------------------------------------------------------------------------------------</para>
        /// <para> SQLBulkCopyWriteThreshold: Default value = [10000] Allowed values:  0 TO Int.MaxValue</para>
        /// <para> SQLExceptionActionModeEnum: Default value = [ExceptionActionModeEnum.LogAndRethrow] Allowed values:  Rethrow, Log, LogAndRethrow .</para>
        /// <para> SQLDBTimeoutRetryCount: Default value = 3.  Allowed values. 0 to DBTimeoutRetryCount * RetryMultiplyValue. Must be less than int.Maxvalue. Retires upto specified times at a rate of increasing interval at each failure.</para>
        /// <para> SQLRetryMultiplyValue: Default value = [10000]. Allowed values. Increment of wait time multiplied by this factor for each retry. Wait time is calculated as DBTimeoutRetryCount * RetryMultiplyValue = eg: 1/2/3/4/5 * 1000 = 1,2,3,4,5 sec or 1/2/3/4/5 * 10000 = 10/20/30/40/50 sec etc.</para>
        /// </summary>
        public static OracleDBBase DB
        {
            get { return _instance; }
        }

    }
}
