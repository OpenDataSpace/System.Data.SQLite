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
      DbProviderFactory fact; // = DbProviderFactories.GetFactory("System.Data.OleDb");
      DbConnection cnn; //  = fact.CreateConnection();

//      cnn.ConnectionString = "Provider=Microsoft.Jet.OLEDB.4.0;Data Source=C:\\Temp\\db.mdb;Persist Security Info=False";
//      cnn.ConnectionString = "Provider=SQLOLEDB.1;Integrated Security=SSPI;Persist Security Info=False;Initial Catalog=DirectLink;Data Source=MASTER";
//      cnn.Open();

//      TestCases.Run(fact, cnn);

      fact = DbProviderFactories.GetFactory("System.Data.SQLite");
      using (cnn = fact.CreateConnection())
      {
        cnn.ConnectionString = "Data Source=test.db3";
        cnn.Open();
        TestCases.Run(fact, cnn);
      }

      System.IO.File.Delete("test.db3");

      Console.ReadKey();
    }
  }
}
