using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Data.SqlClient;
using Oracle.DataAccess.Client;
using System.Data.Common;

namespace DBBaseTest
{

    class OracleDBTest
    {
        public static void TestInterface()
        {
            try
            {
                string sqlconnectionstr = "Data Source=<<SERVER NAME>>;User ID=<<USERID>>;Password=<<PASSWORD>>";
                OracleConnection con = new OracleConnection(sqlconnectionstr);
                decimal cnt;
                string FirstName = "", Lastname = "";
                int Age = 0;


                //Gets a single value after executing query.
                cnt = Ora.DB.ExecuteScalarQuery<decimal>(con, "Select count(*) as cnt from Debug_test", "cnt");
                Console.WriteLine("Gets a single value after executing query." + ", Count : " + cnt.ToString());

                Lastname = Ora.DB.ExecuteScalarStoredProcedure<string>(con, "ADMIN.DEBUG_TEST_GET_SPROC",
                    100, OracleDbType.Varchar2, "vLastName",
                    (cmd) =>
                    {
                        Ora.DB.InsertInParams(cmd, "VId", 1);
                        Ora.DB.InsertOutParams((OracleCommand)cmd, "vFirstName", OracleDbType.Varchar2, 100, "vLastName", OracleDbType.Varchar2, 100);
                    });


                DateTime dt = Ora.DB.ExecuteScalarStoredProcedure<DateTime>(con, "ADMIN.DEBUG_TEST_GET_SPROC",
                    4000, OracleDbType.Date, "vLastName", (cmd) =>
                    {
                        //((OracleCommand)cmd).Parameters.Add("vID", 1);
                        Ora.DB.InsertInParams(cmd, "vID", 1);
                        Ora.DB.InsertOutParams(cmd, "vFirstName", OracleDbType.Varchar2, 100);
                    });

                //executes an update query and returns number of records affected.
                cnt = Ora.DB.ExecuteBasicQuery(con, "update Debug_test set FirstName = 'First3' where ID = 2",
                    (cmd, ex) =>
                    {
                        return "UnExpected error on TestSQLInterface";
                    });
                Console.WriteLine("executes an update query and returns number of records affected." + ", updated rows : " + cnt.ToString());

                //Run procedure and even on excpetion updates up to the exception point.
                cnt = Ora.DB.ExecuteBasicStoredProcedure(con, "DEBUG_TEST_SPROC", (cmd) => Ora.DB.InsertInParams(cmd, "vAge", 10, "vid", 1, "vFirstName", "F2", "vActive", "L1", "vInsertTime", "L1"),
                        (cmd, ex) =>
                        {
                            return "UnExpected error on TestSQLInterface";
                        });
                Console.WriteLine("No transaction. run procedure and even on excpetion updates upto exception point." + ", Results : " + cnt.ToString());


                //Passes values to SPROC and returns values from sproc. with exception handling.
                Ora.DB.ExecuteComplexStoredProcedure<bool>(con, "DEBUG_TEST_GET_SPROC",
                (rdr, cmd) =>
                {
                    while (rdr.Read())
                    {
                        FirstName = rdr["FirstName"].ToString();
                        Lastname = rdr["LastName"].ToString();
                        Age = int.Parse(rdr["Age"].ToString());
                    }
                    return true;
                },
                (cmd) =>
                {
                    Ora.DB.InsertValues(cmd, "ID", 1);
                },
                (cmd,rdr, ex) =>
                {
                    return "Error on passes values to SPROC and returns values from sproc. with exception handling.";
                });


                //Executes a query and returns multiple values at once.
                bool status = Ora.DB.ExecuteComplexQuery<bool>(con, "select * from Debug_test where id = 1",
                    (rdr) =>
                    {
                        while (rdr.Read())
                        {
                            FirstName = rdr["FirstName"].ToString();
                            Lastname = rdr["LastName"].ToString();
                            Age = int.Parse(rdr["Age"].ToString());
                        }
                        return true;
                    });
                Console.WriteLine("Executes a query and returns multiple values at once." + ", Results : " + "FirstName " + FirstName + ", Lastname " + ", Age" + Age);

                //with transaction. No changes will be updated since there is a transaction.
                cnt = Ora.DB.ExecuteBasicStoredProcedure(con, "DEBUG_TEST_SPROC", (cmd) => Ora.DB.InsertValues(cmd, "Age", 10,false, "ID", 1, "FirstName", "F3", "LastName", "L1"),
                    (cmd, ex) =>
                    {
                        return "UnExpected error on TestSQLInterface";
                    }, true);

            }
            catch
            {

            }
        }


    }


/*
    'Sample'  Oracle Server  DB objects. This may have been changed in the samples.
*/


    //CREATE TABLE ADMIN.Debug_Test  ( 
//    FirstName	VARCHAR2(25) NULL,
//    LastName 	VARCHAR2(25) NULL,
//    Age      	NUMBER(15,5) NULL,
//    ID       	NUMBER(15,5) NULL,
//    Active   	NUMBER(15,5) NULL 
//    )
//GO

//GRANT SELECT, INSERT, UPDATE, DELETE ON ADMIN.Debug_Test TO HESINT
//GO

//CREATE OR REPLACE PROCEDURE "ADMIN"."DEBUG_TEST_SPROC"
//(vAge IN INT, vid IN int, vFirstName varchar2, vActive int
//    )
//  IS
//BEGIN

// update ADMIN.Debug_Test set Age = vAge, FirstName = vFirstName where ID = vID;

//END;

//CREATE OR REPLACE PROCEDURE "ADMIN"."DEBUG_TEST_GET_SPROC"
//(vid IN int, vFirstName out varchar2 )
//  IS
//BEGIN

//select FirstName into vFirstName from Debug_test where id = vID;

//END;

}
