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
  public sealed class SQLiteCommand : DbCommand
  {
    private string                    _commandText;
    private SQLiteConnection          _cnn;
    private bool                      _isReaderOpen;
    private int                       _commandTimeout;
    private bool                      _designTimeVisible;
    private UpdateRowSource           _updateRowSource;
    private SQLiteParameterCollection _parameterCollection;

    internal SQLiteStatement[]        _statementList;

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
    /// <param name="strSql">The SQL command text</param>
    public SQLiteCommand(string strSql)
    {
      Initialize(strSql, null);
    }

    /// <summary>
    /// Initializes the command with the given SQL command text and attach the command to the specified
    /// connection.
    /// </summary>
    /// <param name="strSql">The SQL command text</param>
    /// <param name="cnn">The connection to associate with the command</param>
    public SQLiteCommand(string strSql, SQLiteConnection cnn)
    {
      Initialize(strSql, cnn);
    }

    /// <summary>
    /// Initializes the command and associates it with the specified connection.
    /// </summary>
    /// <param name="cnn"></param>
    public SQLiteCommand(SQLiteConnection cnn)
    {
      Initialize(null, cnn);
    }

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
    /// 
    /// </summary>
    /// <param name="disposing"></param>
    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
      ClearCommands();
      _parameterCollection.Clear();
      _cnn = null;
      _commandText = null;
    }

    internal void ClearCommands()
    {
      if (_statementList == null) return;

      int x = _statementList.Length;
      for (int n = 0; n < x; n++)
        _statementList[n].Dispose();

      _statementList = null;

      _parameterCollection.Unbind();
    }

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
          if (itm != null) lst.Add(itm);
        }
      }
      catch (Exception e)
      {
        ClearCommands();
        throw (e);
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

//        if (value == null)
//          throw new ArgumentNullException();

        ClearCommands();
        _commandText = value;

        if (_cnn == null) return;

        BuildCommands();
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
          throw new NotImplementedException();
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

        if (_commandText != null)
          BuildCommands();
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
        if (_cnn == null) return;

        if (value != _cnn._activeTransaction && value != null)
        {
          throw new ArgumentOutOfRangeException();
        }
      }
    }

    private void InitializeForReader()
    {
      if (_isReaderOpen)
        throw new InvalidOperationException("DataReader already active on this command");

      if (_cnn == null)
        throw new InvalidOperationException("No connection associated with this Command");

      if (_cnn.State != ConnectionState.Open)
        throw new InvalidOperationException("Database is not open");

      int n;
      int x;

      if (_statementList.Length == 0)
      {
        BuildCommands();
      }

      // Make sure all parameters are mapped properly to associated statement(s)
      _parameterCollection.MapParameters();

      x = _statementList.Length;
      // Bind all parameters to their statements
      for (n = 0; n < x; n++)
        _statementList[n].BindParameters();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="behavior"></param>
    /// <returns></returns>
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
      InitializeForReader();

      _cnn._sql.SetTimeout(_commandTimeout * 1000);

      try
      {
        SQLiteDataReader rd = new SQLiteDataReader(this, behavior);

        _isReaderOpen = true;
        
        return rd;
      }
      finally
      {
      }
    }

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
      //using (DbDataReader rd = ExecuteDbDataReader(CommandBehavior.Default))
      //{
      //  rd.Close();
      //  return rd.RecordsAffected;
      //}
      InitializeForReader();

      _cnn._sql.SetTimeout(_commandTimeout * 1000);

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
    /// Execute the command and return the first column of the first row of the resultset (if present), or null if no resultset was returned.
    /// </summary>
    /// <returns></returns>
    public override object ExecuteScalar()
    {
      //using (DbDataReader rd = ExecuteDbDataReader(CommandBehavior.Default))
      //{
      //  if (rd.Read())
      //    return rd[0];
      //}
      //return null;

      InitializeForReader();

      _cnn._sql.SetTimeout(_commandTimeout * 1000);

      int n;
      int x;
      object ret = null;
      SQLiteType typ = new SQLiteType();

      x = _statementList.Length;

      for (n = 0; n < x; n++)
      {
        if (_cnn._sql.Step(_statementList[n]) == true)
        {
          ret = _cnn._sql.GetValue(_statementList[n], 0, ref typ);
        }
        _cnn._sql.Reset(_statementList[n]);
        if (ret != null) break;
      }

      if (ret == null) ret = DBNull.Value;

      return ret;
    }

    /// <summary>
    /// Prepares the command for execution.
    /// </summary>
    public override void Prepare()
    {
      if (_statementList.Length == 0)
      {
        BuildCommands();
      }
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
  }
}
