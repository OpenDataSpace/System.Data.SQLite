using System;
using System.Data;
using System.Text;
using System.Data.Common;
using System.Data.SQLite;

namespace test
{
  class Program
  {
    static void Main(string[] args)
    {
      DbProviderFactory fact;
      DbConnection cnn;

      using (cnn = new SQLiteConnection())
      {
        fact = DbProviderFactories.GetFactory("System.Data.SQLite");
        cnn.ConnectionString = "Data Source=test.db3";
        cnn.Open();
        TestCases.Run(fact, cnn);
      }

      System.IO.File.Delete("test.db3");

      Console.ReadKey();
    }
  }
}
