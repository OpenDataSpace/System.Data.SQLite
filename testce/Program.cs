using System;
using System.Data;
using System.Text;
using System.Data.Common;
using System.Data.SQLite;

namespace test
{
  class Program
  {
    [MTAThread]
    static void Main()
    {
      DbConnection cnn;

      //fact = DbProviderFactories.GetFactory("System.Data.SqlClient");
      //using (cnn = fact.CreateConnection())
      //{
      //  cnn.ConnectionString = "Integrated Security=SSPI;Persist Security Info=False;Initial Catalog=dlink;Data Source=(LOCAL)";
      //  cnn.Open();
      //  TestCases.Run(fact, cnn);
      //}
//      cnn.ConnectionString = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=C:\\Temp\\db.mdb;Persist Security Info=False";
//      cnn.ConnectionString = "Provider=SQLOLEDB.1;Integrated Security=SSPI;Persist Security Info=False;Initial Catalog=DirectLink;Data Source=MASTER";
//      cnn.Open();

//      TestCases.Run(fact, cnn);

      SQLiteFunction.RegisterFunction(typeof(TestFunc));
      SQLiteFunction.RegisterFunction(typeof(MyCount));
      SQLiteFunction.RegisterFunction(typeof(MySequence));

      using (cnn = new SQLiteConnection())
      {
        TestCases tests = new TestCases();

        cnn.ConnectionString = "Data Source=test.db3;Synchronous=Off;UseUTF16Encoding=TRUE";
        cnn.Open();
        tests.Run(cnn);

        System.Windows.Forms.Application.Run(tests.frm);
      }

      System.IO.File.Delete("test.db3");
    }
  }
}
