/********************************************************
 * ADO.NET 2.0 Data Provider for SQLite Version 3.X
 * Written by Robert Simpson (robert@blackcastlesoft.com)
 * 
 * Released to the public domain, use at your own risk!
 ********************************************************/

namespace System.Data.SQLite
{
  using System;
  using System.Data;
  using System.Data.Common;

  /// <summary>
  /// SQLite implementation of DbDataAdapter.
  /// </summary>
  public sealed class SQLiteDataAdapter : DbDataAdapter
  {
    /// <overloads>
    /// This class is just a shell around the DbDataAdapter.  Nothing from DbDataAdapter is overridden here, just a few constructors are defined.
    /// </overloads>
    /// <summary>
    /// Default constructor.
    /// </summary>
    public SQLiteDataAdapter()
    {
    }

    /// <summary>
    /// Constructs a data adapter using the specified select command.
    /// </summary>
    /// <param name="cmd">The select command to associate with the adapter.</param>
    public SQLiteDataAdapter(SQLiteCommand cmd)
    {
      SelectCommand = cmd;
    }

    /// <summary>
    /// Constructs a data adapter with the supplied select command text and associated with the specified connection.
    /// </summary>
    /// <param name="CommandText">The select command text to associate with the data adapter.</param>
    /// <param name="cnn">The connection to associate with the select command.</param>
    public SQLiteDataAdapter(string CommandText, SQLiteConnection cnn)
    {
      SelectCommand = new SQLiteCommand(CommandText, cnn);
    }

    /// <summary>
    /// Constructs a data adapter with the specified select command text, and using the specified database connection string.
    /// </summary>
    /// <param name="CommandText">The select command text to use to construct a select command.</param>
    /// <param name="ConnectionString">A connection string suitable for passing to a new SQLiteConnection, which is associated with the select command.</param>
    public SQLiteDataAdapter(string CommandText, string ConnectionString)
    {
      SQLiteConnection cnn = new SQLiteConnection(ConnectionString);
      SelectCommand = new SQLiteCommand(CommandText, cnn);
    }
  }
}
