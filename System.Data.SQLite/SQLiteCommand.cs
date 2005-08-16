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
  using System.Collections.Generic;

  /// <summary>
  /// SQLite implementation of DbCommand.
  /// </summary>
  public sealed class SQLiteCommand : DbCommand, ICloneable
  {
    /// <summary>
    /// The command text this command is based on
    /// </summary>
    private string _commandText;
    /// <summary>
    /// The connection the command is associated with
    /// </summary>
    private SQLiteConnection _cnn;
    /// <summary>
    /// Indicates whether or not a DataReader is active on the command.
    /// </summary>
    private bool _isReaderOpen;
    /// <summary>
    /// The timeout for the command, kludged because SQLite doesn't support per-command timeout values
    /// </summary>
    internal int _commandTimeout;
    /// <summary>
    /// Designer support
    /// </summary>
    private bool _designTimeVisible;
    /// <summary>
    /// Used by DbDataAdapter to determine updating behavior
    /// </summary>
    private UpdateRowSource _updateRowSource;
    /// <summary>
    /// The collection of parameters for the command
    /// </summary>
    private SQLiteParameterCollection _parameterCollection;
    /// <summary>
    /// The SQL command text, broken into individual SQL statements
    /// </summary>
    internal SQLiteStatement[] _statementList;

    ///<overloads>
    /// Constructs a new SQLiteCommand
    /// </overloads>
    /// <summary>
    /// Default constructor
    /// </summary>
    public SQLiteCommand()
    {
      Initialize(null, null);
    }

    /// <summary>
    /// Initializes the command with the given command text
    /// </summary>
    /// <param name="commandText">The SQL command text</param>
    public SQLiteCommand(string commandText)
    {
      Initialize(commandText, null);
    }

    /// <summary>
    /// Initializes the command with the given SQL command text and attach the command to the specified
    /// connection.
    /// </summary>
    /// <param name="commandText">The SQL command text</param>
    /// <param name="cnn">The connection to associate with the command</param>
    public SQLiteCommand(string commandText, SQLiteConnection cnn)
    {
      Initialize(commandText, cnn);
    }

    /// <summary>
    /// Initializes the command and associates it with the specified connection.
    /// </summary>
    /// <param name="cnn">The connection to associate with the command</param>
    public SQLiteCommand(SQLiteConnection cnn)
    {
      Initialize(null, cnn);
    }

    /// <summary>
    /// Initializes a command with the given SQL, connection and transaction
    /// </summary>
    /// <param name="commandText">The SQL command text</param>
    /// <param name="cnn">The connection to associate with the command</param>
    /// <param name="trans">The transaction the command should be associated with</param>
    public SQLiteCommand(string commandText, SQLiteConnection cnn, SQLiteTransaction trans)
    {
      Initialize(commandText, cnn);
      Transaction = trans;
    }

    /// <summary>
    /// Initializes the command class
    /// </summary>
    /// <param name="strSql">The SQL command text</param>
    /// <param name="cnn">A connection to associate with the command</param>
    private void Initialize(string strSql, SQLiteConnection cnn)
    {
      _statementList = null;
      _isReaderOpen = false;
      _commandTimeout = 30;
      _parameterCollection = new SQLiteParameterCollection(this);
      _designTimeVisible = true;
      _updateRowSource = UpdateRowSource.FirstReturnedRecord;

      if (strSql != null)
        CommandText = strSql;

      if (cnn != null)
        DbConnection = cnn;
    }

    /// <summary>
    /// Disposes of the command and clears all member variables
    /// </summary>
    /// <param name="disposing">Whether or not the class is being explicitly or implicitly disposed</param>
    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      ClearCommands();
      _parameterCollection.Clear();
      _cnn = null;
      _commandText = null;
    }

    /// <summary>
    /// Clears and destroys all statements currently prepared
    /// </summary>
    internal void ClearCommands()
    {
      if (_statementList == null) return;

      int x = _statementList.Length;
      for (int n = 0; n < x; n++)
        _statementList[n].Dispose();

      _statementList = null;

      _parameterCollection.Unbind();
    }

    /// <summary>
    /// Builds an array of prepared statements for each complete SQL statement in the command text
    /// </summary>
    internal void BuildCommands()
    {
      ClearCommands();

      if (_cnn.State != ConnectionState.Open) return;

      string strRemain = _commandText;
      SQLiteStatement itm;
      int nStart = 0;
      List<SQLiteStatement> lst = new List<SQLiteStatement>();

      try
      {
        while (strRemain.Length > 0)
        {
          itm = _cnn._sql.Prepare(strRemain, ref nStart, out strRemain);
          if (itm != null)
          {
            itm._command = this;
            lst.Add(itm);
          }
        }
      }
      catch (Exception)
      {
        ClearCommands();
        throw;
      }
      _statementList = new SQLiteStatement[lst.Count];
      lst.CopyTo(_statementList, 0);
    }

    /// <summary>
    /// Not implemented
    /// </summary>
    public override void Cancel()
    {
    }

    /// <summary>
    /// The SQL command text associated with the command
    /// </summary>
    public override string CommandText
    {
      get
      {
        return _commandText;
      }
      set
      {
        if (_commandText == value) return;

        if (_isReaderOpen)
        {
          throw new InvalidOperationException("Cannot set CommandText while a DataReader is active");
        }

        ClearCommands();
        _commandText = value;

        if (_cnn == null) return;
      }
    }

    /// <summary>
    /// The amount of time to wait for the connection to become available before erroring out
    /// </summary>
    public override int CommandTimeout
    {
      get
      {
        return _commandTimeout;
      }
      set
      {
        _commandTimeout = value;
      }
    }

    /// <summary>
    /// The type of the command.  SQLite only supports CommandType.Text
    /// </summary>
    public override CommandType CommandType
    {
      get
      {
        return CommandType.Text;
      }
      set
      {
        if (value != CommandType.Text)
        {
          throw new NotSupportedException();
        }
      }
    }

    /// <summary>
    /// Create a new parameter
    /// </summary>
    /// <returns></returns>
    protected override DbParameter CreateDbParameter()
    {
      return new SQLiteParameter();
    }

    /// <summary>
    /// The connection associated with this command
    /// </summary>
    protected override DbConnection DbConnection
    {
      get
      {
        return _cnn;
      }
      set
      {
        if (_isReaderOpen)
          throw new InvalidOperationException("Cannot set Connection while a DataReader is active");

        if (_cnn != null)
        {
          ClearCommands();
          _cnn._commandList.Remove(this);
        }

        _cnn = (SQLiteConnection)value;
        _cnn._commandList.Add(this);
      }
    }

    /// <summary>
    /// Returns the SQLiteParameterCollection for the given command
    /// </summary>
    protected override DbParameterCollection DbParameterCollection
    {
      get
      {
        return _parameterCollection;
      }
    }

    /// <summary>
    /// The transaction associated with this command.  SQLite only supports one transaction per connection, so this property forwards to the
    /// command's underlying connection.
    /// </summary>
    protected override DbTransaction DbTransaction
    {
      get
      {
        return _cnn._activeTransaction;
      }
      set
      {
        if (_cnn != null)
        {
          if (value != _cnn._activeTransaction && value != null)
            throw new ArgumentOutOfRangeException("DbTransaction", "Transaction is for a different connection than the one associated with this Command");
        }
        else if (value != null)
          throw new ArgumentOutOfRangeException("DbTransaction", "Not associated with a connection");
      }
    }

    /// <summary>
    /// This function ensures there are no active readers, that we have a valid connection,
    /// that the connection is open, that all statements are prepared and all parameters are assigned
    /// in preparation for allocating a data reader.
    /// </summary>
    private void InitializeForReader()
    {
      if (_isReaderOpen)
        throw new InvalidOperationException("DataReader already active on this command");

      if (_cnn == null)
        throw new InvalidOperationException("No connection associated with this command");

      if (_cnn.State != ConnectionState.Open)
        throw new InvalidOperationException("Database is not open");

      // Make sure all statements are prepared
      Prepare();

      // Make sure all parameters are mapped properly to associated statement(s)
      _parameterCollection.MapParameters();

      // Bind all parameters to their statements
      int n;
      int x;

      x = _statementList.Length;
      for (n = 0; n < x; n++)
        _statementList[n].BindParameters();

      // Set the default command timeout
      _cnn._sql.SetTimeout(_commandTimeout * 1000);
    }

    /// <summary>
    /// Creates a new SQLiteDataReader to execute/iterate the array of SQLite prepared statements
    /// </summary>
    /// <param name="behavior">The behavior the data reader should adopt</param>
    /// <returns>Returns a SQLiteDataReader object</returns>
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
      InitializeForReader();

      SQLiteDataReader rd = new SQLiteDataReader(this, behavior);
      _isReaderOpen = true;

      return rd;
    }

    /// <summary>
    /// Overrides the default behavior to return a SQLiteDataReader specialization class
    /// </summary>
    /// <param name="behavior">The flags to be associated with the reader</param>
    /// <returns>A SQLiteDataReader</returns>
    public new SQLiteDataReader ExecuteReader(CommandBehavior behavior)
    {
      return (SQLiteDataReader)ExecuteDbDataReader(behavior);
    }

    /// <summary>
    /// Overrides the default behavior of DbDataReader to return a specialized SQLiteDataReader class
    /// </summary>
    /// <returns>A SQLiteDataReader</returns>
    public new SQLiteDataReader ExecuteReader()
    {
      return (SQLiteDataReader)ExecuteDbDataReader(CommandBehavior.Default);
    }

    /// <summary>
    /// Called by the SQLiteDataReader when the data reader is closed.
    /// </summary>
    internal void ClearDataReader()
    {
      _isReaderOpen = false;
    }

    /// <summary>
    /// Execute the command and return the number of rows inserted/updated affected by it.
    /// </summary>
    /// <returns></returns>
    public override int ExecuteNonQuery()
    {
      InitializeForReader();

      int nAffected = 0;
      int n;
      int x;

      x = _statementList.Length;

      for (n = 0; n < x; n++)
      {
        _cnn._sql.Step(_statementList[n]);
        nAffected += _cnn._sql.Changes;
        _cnn._sql.Reset(_statementList[n]);
      }

      return nAffected;
    }

    /// <summary>
    /// Execute the command and return the first column of the first row of the resultset
    /// (if present), or null if no resultset was returned.
    /// </summary>
    /// <returns>The first column of the first row of the first resultset from the query</returns>
    public override object ExecuteScalar()
    {
      InitializeForReader();

      int n;
      int x;
      object ret = null;
      SQLiteType typ = new SQLiteType();

      x = _statementList.Length;

      for (n = 0; n < x; n++)
      {
        if (_cnn._sql.Step(_statementList[n]) == true && ret == null)
        {
          ret = _cnn._sql.GetValue(_statementList[n], 0, ref typ);
        }
        _cnn._sql.Reset(_statementList[n]);
      }

      if (ret == null) ret = DBNull.Value;

      return ret;
    }

    /// <summary>
    /// Prepares the command for execution.
    /// </summary>
    public override void Prepare()
    {
      if (_statementList != null)
      {
        if (_statementList.Length == 0)
        {
          BuildCommands();
        }
      }
      else
        BuildCommands();
    }

    /// <summary>
    /// Sets the method the SQLiteCommandBuilder uses to determine how to update inserted or updated rows in a DataTable.
    /// </summary>
    public override UpdateRowSource UpdatedRowSource
    {
      get
      {
        return _updateRowSource;
      }
      set
      {
        _updateRowSource = value;
      }
    }

    /// <summary>
    /// Determines if the command is visible at design time.  Defaults to True.
    /// </summary>
    public override bool DesignTimeVisible
    {
      get
      {
        return _designTimeVisible;
      }
      set
      {
        _designTimeVisible = value;
      }
    }

    /// <summary>
    /// Clones a command, including all its parameters
    /// </summary>
    /// <returns>A new SQLiteCommand with the same commandtext, connection and parameters</returns>
    public object Clone()
    {
      SQLiteCommand newcommand = new SQLiteCommand(CommandText, Connection as SQLiteConnection);
      foreach (SQLiteParameter param in _parameterCollection)
      {
        newcommand.Parameters.Add(param.Clone());
      }
      return newcommand;
    }
  }
}