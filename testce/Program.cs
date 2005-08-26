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

      SQLiteFunction.RegisterFunction(typeof(TestFunc));
      SQLiteFunction.RegisterFunction(typeof(MyCount));
      SQLiteFunction.RegisterFunction(typeof(MySequence));

      using (cnn = new SQLiteConnection())
      {
        TestCases tests = new TestCases();

        cnn.ConnectionString = "Data Source=test.db3";
        cnn.Open();
        tests.Run(cnn);

        System.Windows.Forms.Application.Run(tests.frm);
      }

      System.IO.File.Delete("test.db3");
    }
  }
}
