using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DBGeneric
{
    public abstract class DBBase
    {

        protected  int _RetryMultiplyValue = 10000; //increment of wait time multiply factor for each retry. Wait time is calculated as DBTimeoutRetryCount * RetryMultiplyValue = eg: 1/2/3/4/5 * 1000 = 1,2,3,4,5 sec or 1/2/3/4/5 * 5000 = 5/10/15,20,25 sec or 1/2/3/4/5 * 10000 = 10/20/30/40/50 sec etc.
        protected  int _DBTimeoutRetryCount; //how many times need to retry the executoin on timeout before it throws failure.
        protected  ExceptionActionModeEnum _exceptionActionMode;

        /// <summary>
        /// Gets or Sets what need to be the action when an exception occurs. Default value: ExceptionActionModeEnum.LogAndRethrow.
        /// </summary>
        public ExceptionActionModeEnum ExceptionActionMode
        {
            get { return _exceptionActionMode; }
            set { _exceptionActionMode = value; }
        }

        /// <summary>
        /// Gets or Sets RetryMultiplyValue.  Increment of wait time multiplied by this factor for each retry. Wait time is calculated as DBTimeoutRetryCount * RetryMultiplyValue = eg: 1/2/3/4/5 * 1000 = 1,2,3,4,5 sec or 1/2/3/4/5 * 10000 = 10/20/30/40/50 sec etc.
        /// </summary>
        public  int RetryMultiplyValue
        {
            get { return _RetryMultiplyValue; }
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Value must be greater than zero");
                if ((_DBTimeoutRetryCount * _RetryMultiplyValue) > int.MaxValue)
                    throw new ArgumentException("The value must between 1 and " + (int.MaxValue / _DBTimeoutRetryCount).ToString());
                _RetryMultiplyValue = value;
            }
        }

        /// <summary>
        /// Gets or Sets DBTimeoutRetryCount.  Specifies how much times DB need to retry before quit. Default value = 3.  Allowed values. 0 to DBTimeoutRetryCount * RetryMultiplyValue. Must be less than int.Maxvalue.  Wait time is calculated as DBTimeoutRetryCount * RetryMultiplyValue = eg: 1/2/3/4/5 * 1000 = 1,2,3,4,5 sec or 1/2/3/4/5 * 10000 = 10/20/30/40/50 sec etc.
        /// </summary>
        public  int DBTimeoutRetryCount
        {
            get { return _DBTimeoutRetryCount; }
            set
            {
                if (value <= 0)
                    throw new ArgumentException("Value must be greater than zero");
                if ((_RetryMultiplyValue * _DBTimeoutRetryCount) > int.MaxValue)
                    throw new ArgumentException("The value must between 1 and " + (int.MaxValue / _RetryMultiplyValue).ToString());
                _DBTimeoutRetryCount = value;
            }
        }

        /// <summary>
        ///  Retrives the retrun parameter from the IDataReader instance once the execution against db is done.
        /// </summary>
        /// <typeparam name="T">Type of data which is convertable.</typeparam>
        /// <param name="rdr">Instance of reader object. This instance must already be executed against DB and values must be into instance.</param>
        /// <param name="returnParam">Name of retrun parameter column in command object.</param>
        /// <returns>Return value as type specified.</returns>
        virtual protected T GetScalarValue<T>(IDataReader rdr, string returnParam)
        {
            try
            {
                T t = default(T);
                if (rdr.Read() == true)
                {
                    if (rdr[returnParam] is System.DBNull)
                    {
                        return default(T);
                    }
                    else if (UserDefinedConvertionExists<T>(rdr[returnParam], ref t) == false)
                    {
                        t = (T)rdr[returnParam];
                    }
                    return t;
                }
                else
                    return default(T);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException("DBBase.GetScalarValue: Invalid datatype detected in Generic type convertion. Expected type " + rdr[returnParam].GetType().ToString(), ex);
            }
        }

        /// <summary>
        /// Retrives the retrun parameter from the SqlCommand once the execution against db is done.
        /// </summary>
        /// <typeparam name="T">Type of data which is convertable.</typeparam>
        /// <param name="cmd">Instance of command object. This instance must already be executed against DB and values must be into instance.</param>
        /// <param name="returnParam">Name of retrun parameter column in command object.</param>
        /// <returns>Return value as type specified.</returns>
        virtual protected T GetScalarValue<T>(DbCommand cmd, string returnParam) 
        {
            try
            {
                if (cmd.Parameters[returnParam].Direction == ParameterDirection.Output && cmd.Parameters[returnParam].Value != DBNull.Value)
                {
                    T t = default(T);
                    if (UserDefinedConvertionExists<T>(cmd.Parameters[returnParam].Value, ref t) == false)
                    {
                        t = (T)cmd.Parameters[returnParam].Value;
                    }
                    return t;
                }
                else
                    return default(T);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException("DBBase.GetScalarValue: Invalid datatype detected in Generic type convertion. Expected type " + cmd.Parameters[returnParam].GetType().ToString(), ex);
            }
        }

        virtual protected T GetScalarValue<T>(DbParameter param)
        {
            try
            {
                if ((param.Direction == ParameterDirection.Output || param.Direction == ParameterDirection.ReturnValue) && param.Value.ToString() != "null" && param.Value != DBNull.Value)
                {
                    T t = default(T);
                    if (UserDefinedConvertionExists<T>(param.Value, ref t) == false)
                    {
                        t = (T)param.Value;
                    }
                    return (T)t;
                }
                else
                    return default(T);
            }
            catch (Exception ex)
            {
                throw new InvalidCastException("DBBase.GetScalarValue: Invalid datatype detected in Generic type convertion. Expected type " + param.GetType().ToString(), ex);
            }
        }



        public virtual T InsertReturnParameter<T>(DbCommand cmd, int outParamSize) where T : DbParameter, new()
        {
            T returnValue;
            if (cmd.CommandType != CommandType.StoredProcedure)
                throw new ArgumentException("Invalid CommandType specified. A parameter can be used only with Storedprocedure or function");
            returnValue = new T();
            returnValue.Direction = ParameterDirection.ReturnValue;
            returnValue.Size = outParamSize;
            cmd.Parameters.Add(returnValue);
            return returnValue;
        }
        
        protected virtual bool InsertOutParameter(DbCommand cmd, string param = "", Enum paramType = null, int? paramSize = null)
        {
            if (param == null || param == "") return false;
            DbParameter p = null;
            if (cmd.Parameters[param] != null)
            {
                p = cmd.Parameters[param];
                if (paramSize.HasValue == true && paramSize.Value > 0)
                    p.Size = paramSize.Value;
                p.Direction = ParameterDirection.Output;
            }
            else
            {
                p = CreatePramaterInstance(param, paramSize, paramType);
                p.Direction = ParameterDirection.Output;
                cmd.Parameters.Add(p);
            }
            return true;
        }

        public  T RetriveOutParameterValue<T>(DbCommand cmd, string outParamName)
        {
            using (cmd)
            {
                if (cmd.Parameters[outParamName] == null)
                    throw new ArgumentException("Invalid outParamName specified. outParamName must be a valid Procedure paramater. check the procedure for paramater name and make sure it is a valid out paramater");
                DbParameter param = cmd.Parameters[outParamName];
                if (param.Direction == ParameterDirection.Input)
                    throw new ArgumentException("Invalid outParamName specified. outParamName must be a valid output or return Procedure paramater. check the procedure for paramater name and make sure it is a valid out paramater");
                T value = GetScalarValue<T>(cmd, outParamName);
                return value;
            }
        }

        public void InsertValues(DbCommand cmd, string param, object value, bool autoGenerateParameters = false, string param1 = "", object value1 = null, string param2 = "", object value2 = null, string param3 = "", object value3 = null
            , string param4 = "", object value4 = null, string param5 = "", object value5 = null, string param6 = "", object value6 = null, string param7 = "", object value7 = null, string param8 = "", object value8 = null
            , string param9 = "", object value9 = null, string param10 = "", object value10 = null, string param11 = "", object value11 = null, string param12 = "", object value12 = null, string param13 = "", object value13 = null
            , string param14 = "", object value14 = null, string param15 = "", object value15 = null, string param16 = "", object value16 = null, string param17 = "", object value17 = null, string param18 = "", object value18 = null
            , string param19 = "", object value19 = null, string param20 = "", object value20 = null, string param21 = "", object value21 = null, string param22 = "", object value22 = null, string param23 = "", object value23 = null
            , string param24 = "", object value24 = null, string param25 = "", object value25 = null, string param26 = "", object value26 = null, string param27 = "", object value27 = null, string param28 = "", object value28 = null
            , string param29 = "", object value29 = null, string param30 = "", object value30 = null, string param31 = "", object value31 = null, string param32 = "", object value32 = null, string param33 = "", object value33 = null
            , string param34 = "", object value34 = null, string param35 = "", object value35 = null, string param36 = "", object value36 = null, string param37 = "", object value37 = null, string param38 = "", object value38 = null
            , string param39 = "", object value39 = null, string param40 = "", object value40 = null)
        {
            try
            {
                if (autoGenerateParameters == true)
                    AutoGenerateCommandParametersFromDB(cmd);
                if (param == "") return;
                cmd.Parameters[param].Value = FormatForDB(value);
                if (param1 == "") { return; }
                cmd.Parameters[param1].Value = FormatForDB(value1);
                if (param2 == "") { return; }
                cmd.Parameters[param2].Value = FormatForDB(value2);
                if (param3 == "") { return; }
                cmd.Parameters[param3].Value = FormatForDB(value3);
                if (param4 == "") { return; }
                cmd.Parameters[param4].Value = FormatForDB(value4);
                if (param5 == "") { return; }
                cmd.Parameters[param5].Value = FormatForDB(value5);
                if (param6 == "") { return; }
                cmd.Parameters[param6].Value = FormatForDB(value6);
                if (param7 == "") { return; }
                cmd.Parameters[param7].Value = FormatForDB(value7);
                if (param8 == "") { return; }
                cmd.Parameters[param8].Value = FormatForDB(value8);
                if (param9 == "") { return; }
                cmd.Parameters[param9].Value = FormatForDB(value9);
                if (param10 == "") { return; }
                cmd.Parameters[param10].Value = FormatForDB(value10);
                if (param11 == "") { return; }
                cmd.Parameters[param11].Value = FormatForDB(value11);
                if (param12 == "") { return; }
                cmd.Parameters[param12].Value = FormatForDB(value12);
                if (param13 == "") { return; }
                cmd.Parameters[param13].Value = FormatForDB(value13);
                if (param14 == "") { return; }
                cmd.Parameters[param14].Value = FormatForDB(value14);
                if (param15 == "") { return; }
                cmd.Parameters[param15].Value = FormatForDB(value15);
                if (param16 == "") { return; }
                cmd.Parameters[param16].Value = FormatForDB(value16);
                if (param17 == "") { return; }
                cmd.Parameters[param17].Value = FormatForDB(value17);
                if (param18 == "") { return; }
                cmd.Parameters[param18].Value = FormatForDB(value18);
                if (param19 == "") { return; }
                cmd.Parameters[param19].Value = FormatForDB(value19);
                if (param20 == "") { return; }
                cmd.Parameters[param20].Value = FormatForDB(value20);
                if (param21 == "") { return; }
                cmd.Parameters[param21].Value = FormatForDB(value21);
                if (param22 == "") { return; }
                cmd.Parameters[param22].Value = FormatForDB(value22);
                if (param23 == "") { return; }
                cmd.Parameters[param23].Value = FormatForDB(value23);
                if (param24 == "") { return; }
                cmd.Parameters[param24].Value = FormatForDB(value24);
                if (param25 == "") { return; }
                cmd.Parameters[param25].Value = FormatForDB(value25);
                if (param26 == "") { return; }
                cmd.Parameters[param26].Value = FormatForDB(value26);
                if (param27 == "") { return; }
                cmd.Parameters[param27].Value = FormatForDB(value27);
                if (param28 == "") { return; }
                cmd.Parameters[param28].Value = FormatForDB(value28);
                if (param29 == "") { return; }
                cmd.Parameters[param29].Value = FormatForDB(value29);
                if (param30 == "") { return; }
                cmd.Parameters[param30].Value = FormatForDB(value30);
                if (param31 == "") { return; }
                cmd.Parameters[param31].Value = FormatForDB(value31);
                if (param32 == "") { return; }
                cmd.Parameters[param32].Value = FormatForDB(value32);
                if (param33 == "") { return; }
                cmd.Parameters[param33].Value = FormatForDB(value33);
                if (param34 == "") { return; }
                cmd.Parameters[param34].Value = FormatForDB(value34);
                if (param35 == "") { return; }
                cmd.Parameters[param35].Value = FormatForDB(value35);
                if (param36 == "") { return; }
                cmd.Parameters[param36].Value = FormatForDB(value36);
                if (param37 == "") { return; }
                cmd.Parameters[param37].Value = FormatForDB(value37);
                if (param38 == "") { return; }
                cmd.Parameters[param38].Value = FormatForDB(value38);
                if (param39 == "") { return; }
                cmd.Parameters[param39].Value = FormatForDB(value39);
                if (param40 == "") { return; }
                cmd.Parameters[param40].Value = FormatForDB(value40);
            }
            catch (Exception ex)
            {
                Logging.AddExceptionEntry(ex, "Invalid data or argument information found in OracleDBBase.InsertParamValues", cmd.CommandText,
                    param, value, param1, value1, param2, value2, param3, value3, param4, value4, param5, value5, param6, value6, param7, value7,
                    param8, value8, param9, value9, param10, value10,
                    param11, value11, param12, value12, param13, value13, param14, value14, param15, value15, param16, value16, param17, value17,
                    param18, value18, param19, value19, param20, value20, param21, value21, param22, value22, param23, value23, param24, value24,
                    param25, value25, param26, value26, param27, value27, param28, value28, param29, value29, param30, value30, param31, value31,
                    param32, value32, param33, value33, param34, value34, param35, value35, param36, value36, param37, value37, param38, value38,
                    param39, value39, param40, value40);
                throw;
            }
        }


        public  void InsertInParams(dynamic cmd, string param, object value, string param1 = "", object value1 = null, string param2 = "", object value2 = null, string param3 = "", object value3 = null
            , string param4 = "", object value4 = null, string param5 = "", object value5 = null, string param6 = "", object value6 = null, string param7 = "", object value7 = null, string param8 = "", object value8 = null
            , string param9 = "", object value9 = null, string param10 = "", object value10 = null, string param11 = "", object value11 = null, string param12 = "", object value12 = null, string param13 = "", object value13 = null
            , string param14 = "", object value14 = null, string param15 = "", object value15 = null, string param16 = "", object value16 = null, string param17 = "", object value17 = null, string param18 = "", object value18 = null
            , string param19 = "", object value19 = null, string param20 = "", object value20 = null, string param21 = "", object value21 = null, string param22 = "", object value22 = null, string param23 = "", object value23 = null
            , string param24 = "", object value24 = null, string param25 = "", object value25 = null, string param26 = "", object value26 = null, string param27 = "", object value27 = null, string param28 = "", object value28 = null
            , string param29 = "", object value29 = null, string param30 = "", object value30 = null, string param31 = "", object value31 = null, string param32 = "", object value32 = null, string param33 = "", object value33 = null
            , string param34 = "", object value34 = null, string param35 = "", object value35 = null, string param36 = "", object value36 = null, string param37 = "", object value37 = null, string param38 = "", object value38 = null
            , string param39 = "", object value39 = null, string param40 = "", object value40 = null)
        {
            try
            {
                //used dynamic since the methods are exactly same  except that it is two different calsses altogether for Oracle and SQL server. The same can be achived using Generic.
                if (param == "" || param ==null) { return; }
                cmd.Parameters.Add(param, FormatForDB(value));
                if (param1 == "" || param1 == null) { return; }
                cmd.Parameters.Add(param1, FormatForDB(value1));
                if (param2 == "" || param2 == null) { return; }
                cmd.Parameters.Add(param2, FormatForDB(value2));
                if (param3 == "" || param3 == null) { return; }
                cmd.Parameters.Add(param3, FormatForDB(value3));
                if (param4 == "" || param4 == null) { return; }
                cmd.Parameters.Add(param4, FormatForDB(value4));
                if (param5 == "" || param5 == null) { return; }
                cmd.Parameters.Add(param5, FormatForDB(value5));
                if (param6 == "" || param6 == null) { return; }
                cmd.Parameters.Add(param6, FormatForDB(value6));
                if (param7 == "" || param7 == null) { return; }
                cmd.Parameters.Add(param7, FormatForDB(value7));
                if (param8 == "" || param8 ==null) { return; }
                cmd.Parameters.Add(param8, FormatForDB(value8));
                if (param9 == "" || param9 ==null) { return; }
                cmd.Parameters.Add(param9, FormatForDB(value9));
                if (param10 == "" || param10 ==null) { return; }
                cmd.Parameters.Add(param10, FormatForDB(value10));
                if (param11 == "" || param11 ==null) { return; }
                cmd.Parameters.Add(param11, FormatForDB(value11));
                if (param12 == "" || param12 ==null) { return; }
                cmd.Parameters.Add(param12, FormatForDB(value12));
                if (param13 == "" || param13 ==null) { return; }
                cmd.Parameters.Add(param13, FormatForDB(value13));
                if (param14 == "" || param14 ==null) { return; }
                cmd.Parameters.Add(param14, FormatForDB(value14));
                if (param15 == "" || param15 ==null) { return; }
                cmd.Parameters.Add(param15, FormatForDB(value15));
                if (param16 == "" || param16 ==null) { return; }
                cmd.Parameters.Add(param16, FormatForDB(value16));
                if (param17 == "" || param17 ==null) { return; }
                cmd.Parameters.Add(param17, FormatForDB(value17));
                if (param18 == "" || param18 ==null) { return; }
                cmd.Parameters.Add(param18, FormatForDB(value18));
                if (param19 == "" || param19 ==null) { return; }
                cmd.Parameters.Add(param19, FormatForDB(value19));
                if (param20 == "" || param20 ==null) { return; }
                cmd.Parameters.Add(param20, FormatForDB(value20));
                if (param21 == "" || param21 ==null) { return; }
                cmd.Parameters.Add(param21, FormatForDB(value21));
                if (param22 == "" || param22 ==null) { return; }
                cmd.Parameters.Add(param22, FormatForDB(value22));
                if (param23 == "" || param23 ==null) { return; }
                cmd.Parameters.Add(param23, FormatForDB(value23));
                if (param24 == "" || param24 ==null) { return; }
                cmd.Parameters.Add(param24, FormatForDB(value24));
                if (param25 == "" || param25 ==null) { return; }
                cmd.Parameters.Add(param25, FormatForDB(value25));
                if (param26 == "" || param26 ==null) { return; }
                cmd.Parameters.Add(param26, FormatForDB(value26));
                if (param27 == "" || param27 ==null) { return; }
                cmd.Parameters.Add(param27, FormatForDB(value27));
                if (param28 == "" || param28 ==null) { return; }
                cmd.Parameters.Add(param28, FormatForDB(value28));
                if (param29 == "" || param29 ==null) { return; }
                cmd.Parameters.Add(param29, FormatForDB(value29));
                if (param30 == "" || param30 ==null) { return; }
                cmd.Parameters.Add(param30, FormatForDB(value30));
                if (param31 == "" || param31 ==null) { return; }
                cmd.Parameters.Add(param31, FormatForDB(value31));
                if (param32 == "" || param32 ==null) { return; }
                cmd.Parameters.Add(param32, FormatForDB(value32));
                if (param33 == "" || param33 ==null) { return; }
                cmd.Parameters.Add(param33, FormatForDB(value33));
                if (param34 == "" || param34 ==null) { return; }
                cmd.Parameters.Add(param34, FormatForDB(value34));
                if (param35 == "" || param35 ==null) { return; }
                cmd.Parameters.Add(param35, FormatForDB(value35));
                if (param36 == "" || param36 ==null) { return; }
                cmd.Parameters.Add(param36, FormatForDB(value36));
                if (param37 == "" || param37 ==null) { return; }
                cmd.Parameters.Add(param37, FormatForDB(value37));
                if (param38 == "" || param38 ==null) { return; }
                cmd.Parameters.Add(param38, FormatForDB(value38));
                if (param39 == "" || param39 ==null) { return; }
                cmd.Parameters.Add(param39, FormatForDB(value39));
                if (param40 == "" || param40 ==null) { return; }
                cmd.Parameters.Add(param40, FormatForDB(value40));
            }
            catch (Exception ex)
            {
                Logging.AddExceptionEntry(ex, "Invalid data or argument information found in OracleDBBase.InsertParams", cmd.CommandText,
                    param, value, param1, value1, param2, value2, param3, value3, param4, value4, param5, value5, param6, value6, param7, value7,
                    param8, value8, param9, value9, param10, value10,
                    param11, value11, param12, value12, param13, value13, param14, value14, param15, value15, param16, value16, param17, value17,
                    param18, value18, param19, value19, param20, value20, param21, value21, param22, value22, param23, value23, param24, value24,
                    param25, value25, param26, value26, param27, value27, param28, value28, param29, value29, param30, value30, param31, value31,
                    param32, value32, param33, value33, param34, value34, param35, value35, param36, value36, param37, value37, param38, value38,
                    param39, value39, param40, value40);
                throw;
            }
        }

        protected  virtual object FormatForDB(object value)
        {
            if (value == null || value == DBNull.Value)
            {
                throw new Exception("FormatForDB: Cannot pass null value into a stored procedure paramaters. Use default values of datatype instead");
            }
            else if (value is bool)
            {
                if ((bool)value == true)
                    return 1;
                else
                    return 0;
            }
            else if (value is string)
            {
                if ((string)value == string.Empty)
                    return "";
                else
                    return value;
            }
            else
            {
                return value;
            }
        }

        #region EXCEPTION HANDLERS

        protected void HandleExceptionRetry(Exception ex, ref int retrycnt, ref int maxretry, DateTime starttime, DbCommand cmd, Func<DbCommand, Exception, string> OnException = null)
        {
            HandleExceptionRetryGeneric(ex, ref retrycnt, ref maxretry, starttime, cmd, OnException: OnException);
        }

        protected void HandleExceptionRetry(Exception ex, ref int retrycnt, ref int maxretry, DateTime starttime, DbCommand cmd, DbDataReader rdr, Func<DbCommand, DbDataReader, Exception, string> OnException = null)
        {
            HandleExceptionRetryGeneric(ex, ref retrycnt, ref maxretry, starttime, cmd, rdr, OnExceptionWithRdr: OnException);
        }

        protected virtual void HandleExceptionRetryGeneric(Exception ex, ref int retrycnt, ref int maxretry, DateTime starttime, DbCommand cmd, DbDataReader rdr = null, Func<DbCommand, Exception, string> OnException = null, Func<DbCommand, DbDataReader, Exception, string> OnExceptionWithRdr = null)
        {
            if (ex.Message.Contains("Timeout expired.") == true)
            {
                if (retrycnt < (maxretry - 1))
                {
                    Logging.AddLogEntry("Timeout Exception occured. Last call start time: " + starttime.ToString(Consts.DOTNET_DATETIMEFORMAT) + ", end time: " + DateTime.Now.ToString(Consts.DOTNET_DATETIMEFORMAT) + ". Start Retry(" + (1 + retrycnt).ToString() + ").");
                    Task.Delay((retrycnt + 1) * _RetryMultiplyValue); //increase wait time on each exception. retryincrementvalue = 10000
                    retrycnt++;
                }
                else
                {
                    string additionaldebuginfo = "";
                    if (OnException != null)
                        additionaldebuginfo = ". Additional Debuginfo: " + OnException(cmd, ex);
                    else if (OnExceptionWithRdr != null && rdr != null)
                        additionaldebuginfo = ". Additional Debuginfo: " + OnExceptionWithRdr(cmd, rdr, ex);
                    string executionParams = ". Execution Parameters: " + GetAllUsedCommandParametersAsString(cmd);
                    Exception subex = new Exception("Exception occured. Maximum attempts (" + maxretry.ToString() + ") completed. If this occurs for more frequently then extend Connect Timeout property for connection. Start Datetime: " + starttime.ToString(Consts.DOTNET_DATETIMEFORMAT) + ", end time: " + DateTime.Now.ToString(Consts.DOTNET_DATETIMEFORMAT) + executionParams + additionaldebuginfo, ex);
                    retrycnt = maxretry;
                    HandleExceptionThrow(subex);
                }
            }
            else
            {
                string additionaldebuginfo = "";
                if (OnException != null)
                    additionaldebuginfo = ". Additional Debuginfo: " + OnException(cmd, ex);
                else if (OnExceptionWithRdr != null && rdr != null)
                    additionaldebuginfo = ". Additional Debuginfo: " + OnExceptionWithRdr(cmd, rdr, ex);
                string executionParams = "Execution Parameters: " + GetAllUsedCommandParametersAsString(cmd);
                Exception subex = new Exception("Unexpected exception occured during database query. " + executionParams + additionaldebuginfo, ex);
                retrycnt = maxretry;
                HandleExceptionThrow(subex);
            }
        }

        protected  virtual void HandleExceptionThrow(Exception ex)
        {
            if (ExceptionActionMode == ExceptionActionModeEnum.LogAndRethrow)
            {
                Logging.AddExceptionEntry(ex);
                throw ex;
            }
            else if (ExceptionActionMode == ExceptionActionModeEnum.Log)
                Logging.AddExceptionEntry(ex);
            else if (ExceptionActionMode == ExceptionActionModeEnum.Rethrow)
                throw ex;
        }

        #endregion

        #region ABSTRACT METHODS
        protected abstract bool UserDefinedConvertionExists<T>(object value, ref T t);
        protected abstract DbParameter CreatePramaterInstance(string param, int? paramSize = null, Enum paramType = null);
        protected abstract string GetAllUsedCommandParametersAsString(DbCommand cmd);
        protected abstract DbConnection CreateConnection(string connectionStr, int customTimeoutValue = -1);
        public abstract T ExecuteScalarQuery<T>(DbConnection connection, string sql, string returnParamName, Func<DbCommand, Exception, string> onException = null) ;
        public abstract T ExecuteScalarQuery<T>(string connectionStr, string sql, string returnParamName, Func<DbCommand, Exception, string> onException = null, int customTimeoutValue = -1);
        public abstract int ExecuteBasicQuery(string connectionStr, string sql, Func<DbCommand, Exception, string> onException = null, int customTimeoutValue = -1);
        public abstract int ExecuteBasicQuery(DbConnection connection, string sql, Func<DbCommand, Exception, string> onException = null);
        public abstract T ExecuteComplexQuery<T>(DbConnection connection, string sql, Func<DbDataReader, T> transformlogic, Func<DbCommand, DbDataReader, Exception, string> onException = null);
        public abstract T ExecuteComplexQuery<T>(string connectionStr, string sql, Func<DbDataReader, T> transformlogic, Func<DbCommand, DbDataReader, Exception, string> onException = null, int customTimeoutValue = -1);
        public abstract int ExecuteBasicStoredProcedure(DbConnection connection, string procedureName, Action<DbCommand> commandParams = null, Func<DbCommand, Exception, string> onException = null, bool doSimpleTransaction = false);
        public abstract int ExecuteBasicStoredProcedure(string connectionStr, string procedureName, Action<DbCommand> commandParams = null, Func<DbCommand, Exception, string> onException = null, bool doSimpleTransaction = false, int customTimeoutValue = -1);
        public abstract T ExecuteScalarStoredProcedure<T>(string connectionStr, string procedureName, int outParamSize, Enum outParamDbType, string outParamName = null,Action<DbCommand> commandParams = null,  Func<DbCommand, Exception, string> onException = null, bool doSimpleTransaction = false, int customTimeoutValue = -1);
        public abstract T ExecuteScalarStoredProcedure<T>(DbConnection connection, string procedureName, int outParamSize, Enum outParamDbType, string outParamName = null, Action<DbCommand> commandParams = null, Func<DbCommand, Exception, string> onException = null, bool doSimpleTransaction = false);
        public abstract T ExecuteComplexStoredProcedure<T>(DbConnection connection, string procedureName, Func<DbDataReader, DbCommand, T> transformLogic, Action<DbCommand> commandParams = null, Func<DbCommand, DbDataReader, Exception, string> onException = null, bool doSimpleTransaction = false);
        public abstract T ExecuteComplexStoredProcedure<T>(string connectionStr, string procedureName, Func<DbDataReader, DbCommand, T> transformLogic, Action<DbCommand> commandParams = null, Func<DbCommand, DbDataReader, Exception, string> onException = null, bool doSimpleTransaction = false, int customTimeoutValue = -1);
        public abstract bool AutoGenerateCommandParametersFromDB(DbCommand cmd);
        #endregion 
    }

    public class Consts
    {
        public const string SQLSERVER_DATETIMEFORMAT = "MM/dd/yyyy hh:mm:ss.fff tt";
        public const string SQLSERVER_DATETIMEFORMAT24hr = "MM-dd-yyyy HH:mm:ss.fff";
        public const string SQLSERVER_DATETIMEFORMATNoMilliSecs = "MM-dd-yyyy HH:mm:ss";
        public const string SQLSERVER_DATEFORMAT = "MM/dd/yyyy";

        public const string ORACLE_DATEFORMAT = "mm/dd/yyyy";
        public const string ORACLE_DATETIMEFORMAT = "mm/dd/yyyy hh:mi:ss.fff AM";

        public const string DOTNET_DATEFORMAT = "MM/dd/yyyy";
        public const string DOTNET_DATETIMEFORMAT = "MM/dd/yyyy hh:mm:ss.fff tt";
        public const string ORACLE_TIMESTAMPFORMAT = "dd-MMM-yy hh.mm.ss.ffffff tt";
    }

    public enum ExceptionActionModeEnum
    {
        Rethrow,
        Log,
        LogAndRethrow
    };
}
