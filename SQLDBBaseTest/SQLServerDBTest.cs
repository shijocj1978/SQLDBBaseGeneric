using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using System.Data.SqlClient;
using System.Data;

namespace DBBaseTest
{
    class SQLServerDBTest
    {
        public static void TestInterface()
        {
            try
            {
                string sqlconnectionstr = "Data Source=<<DATASOURCENAME>>;Initial Catalog=<<DBNAME>>;Integrated Security=True";
                SqlConnection con = new SqlConnection(sqlconnectionstr);
                int cnt;
                string FirstName = "", Lastname = "";
                int Age = 0;
                DateTime DOB = DateTime.Now;

                //gets a single value after executing query.
                cnt = Sql.DB.ExecuteScalarQuery<int>(con, "Select count(*) as cnt from Debug_test", "cnt");
                Console.WriteLine("Gets a single value after executing query." + ", Count : " + cnt.ToString());


                //grabbing a string out value
                FirstName = Sql.DB.ExecuteScalarStoredProcedure<string>(con, "DEBUG_TEST_GET_SPROC",100,SqlDbType.VarChar,"FirstName",
                    (cmd) =>
                    {
                        Sql.DB.InsertInParams(cmd, "ID", 1);
                        Sql.DB.InsertOutParams(cmd, "FirstName", System.Data.SqlDbType.NVarChar, 100, "LastName", SqlDbType.NVarChar, 100, "DOB", SqlDbType.DateTime, 0); //use this incase you have additional out params.
                    }
                    , doSimpleTransaction: true);

                //executes an update query and returns number of records affected.
                cnt = Sql.DB.ExecuteBasicQuery(con, "update Debug_test set FirstName = 'First3' where ID = 2; update Debug_test set FirstName = 'First3' where ID = 5",
                    (cmd, ex) =>
                    {
                        return "UnExpected error on TestSQLInterface";
                    });
                Console.WriteLine("executes an update query and returns number of records affected." + ", updated rows : " + cnt.ToString());

                //Run procedure and even on excpetion updates up to the exception point.
                cnt = Sql.DB.ExecuteBasicStoredProcedure(con, "DEBUG_TEST_SPROC", (cmd) => Sql.DB.InsertInParams(cmd, "Age", 10, "ID", 1, "FirstName", "F2", "Active", 1),
                        (cmd, ex) =>
                        {
                            return "UnExpected error on TestSQLInterface";
                        });


                //Passes values to SPROC and returns values from sproc. with exception handling.
                Sql.DB.ExecuteComplexStoredProcedure<bool>(con, "DEBUG_TEST_GET_SPROC",
                (rdr, cmd) =>
                {
                    FirstName = cmd.Parameters["FirstName"].Value.ToString();
                    Lastname = cmd.Parameters["LastName"].Value.ToString();
                    DOB = DateTime.Parse(cmd.Parameters["DOB"].Value.ToString());
                    return true;
                },
                (cmd) =>
                {
                    Sql.DB.InsertInParams(cmd, "ID", 1);
                    Sql.DB.InsertOutParams(cmd, "FirstName", System.Data.SqlDbType.NVarChar, 100, "LastName", SqlDbType.NVarChar, 100, "DOB", SqlDbType.DateTime, 0); //this is the pure way to get it done but even giving the above works for SQLserver.
                },
                (cmd, rdr, ex) =>
                {
                    return "Error on passes values to SPROC and returns values from sproc. with exception handling.";
                });

                //Executes a query and returns multiple values at once.
                bool status = Sql.DB.ExecuteComplexQuery<bool>(con, "select * from Debug_test where id = 1",
                    (rdr) =>
                    {
                        while (rdr.Read())
                        {
                            FirstName = rdr["FirstName"].ToString();
                            Lastname = rdr["LastName"].ToString();
                            Age = int.Parse(rdr["Age"].ToString());
                        }
                        return true;
                    },
                    (cmd,rdr,ex)=>
                    {
                        return cmd.CommandText + rdr.GetValue(0).ToString() + ex.Message;
                    });
                Console.WriteLine("Executes a query and returns multiple values at once." + ", Results : " + "FirstName " + FirstName + ", Lastname " + ", Age" + Age);

                //with transaction. No changes will be updated from first step in sproc since there is a transaction.
                cnt = Sql.DB.ExecuteBasicStoredProcedure(con, "DEBUG_TEST_SPROC", (cmd) => Sql.DB.InsertValues(cmd, "Age", 10,false, "ID", 1, "FirstName", "F3", "Active", "1"),
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
    'Sample' SQL Server DB objects. This may have been changed in the samples.
*/

//USE [DBO]
//GO
///****** Object:  StoredProcedure dbo.[DEBUG_TEST_SPROC]    Script Date: 11/10/2015 13:49:11 ******/
//SET ANSI_NULLS ON
//GO
//SET QUOTED_IDENTIFIER ON
//GO

    //ALTER procedure  [dbo].[DEBUG_TEST_SPROC]
//(
//  @Age int,
//  @ID  int,
//  @FirstName             varchar(50) = Null
//)
//as
//begin

//update Debug_test set Age = @Age, Active = 'F', FirstName = @FirstName where ID = @ID;

//END


//    USE [DBO]
//GO

//SET ANSI_NULLS ON
//GO
//SET QUOTED_IDENTIFIER ON
//GO

//alter procedure  [DEBUG_TEST_GET_SPROC]
//(
//  @ID  int
//)
//as
//begin

//select * from Debug_test where id = @ID;

//END
//GO

//USE [DBO]+8794

    ///****** Object:  Table [dbo].[Debug_Test]    Script Date: 11/10/2015 16:00:30 ******/
//SET ANSI_NULLS ON
//GO

//SET QUOTED_IDENTIFIER ON
//GO

    //CREATE TABLE [dbo].[Debug_Test](
//    [FirstName] [nvarchar](50) NULL,
//    [LastName] [nvarchar](50) NULL,
//    [Age] [int] NULL,
//    [ID] [int] NULL,
//    [Active] [bit] NULL
//) ON [PRIMARY]

//GO

    //Table Debug_Test values.
//FirstName LastName Age ID Active
//First1	Last1	1	1	True
//First2	Last2	2	2	True

}
