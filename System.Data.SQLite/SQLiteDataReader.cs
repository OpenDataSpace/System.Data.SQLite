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

  internal struct SQLiteType
  {
    internal DbType         Type;
    internal TypeAffinity Affinity;
  }

  /// <summary>
  /// SQLite implementation of DbDataReader.
  /// </summary>
  public sealed class SQLiteDataReader : DbDataReader
  {
    /// <summary>
    /// Underlying command this reader is attached to
    /// </summary>
    private SQLiteCommand   _command;
    /// <summary>
    /// Index of the current statement in the command being processed
    /// </summary>
    private int             _activeStatementIndex;
    /// <summary>
    /// Current statement being Read()
    /// </summary>
    private SQLiteStatement _activeStatement;
    /// <summary>
    /// State of the current statement being processed.
    /// -1 = First Step() executed, so the first Read() will be ignored
    ///  0 = Actively reading
    ///  1 = Finished reading
    ///  2 = Non-row-returning statement, no records
    /// </summary>
    private int             _readingState;
    /// <summary>
    /// Number of records affected by the insert/update statements executed on the command
    /// </summary>
    private int             _rowsAffected;
    /// <summary>
    /// Count of fields (columns) in the row-returning statement currently being processed
    /// </summary>
    private int             _fieldCount;
    /// <summary>
    /// Datatypes of active fields (columns) in the current statement, used for type-restricting data
    /// </summary>
    private SQLiteType[]    _fieldTypeArray;

    /// <summary>
    /// The behavior of the datareader
    /// </summary>
    private CommandBehavior _commandBehavior;

    internal SQLiteDataReader(SQLiteCommand cmd, CommandBehavior behave)
    {
      _command = cmd;
      _commandBehavior = behave;
      Initialize();
    }

    internal void Initialize()
    {
      _activeStatementIndex = -1;
      _activeStatement = null;
      _rowsAffected = -1;
      _fieldCount = -1;

      NextResult();
    }

    /// <summary>
    /// Closes the datareader, potentially closing the connection as well if CommandBehavior.CloseConnection was specified.
    /// </summary>
    public override void Close()
    {
      if (_command != null)
      {
        while (NextResult()) ;
        _command.ClearDataReader();
      }

      // If the datareader's behavior includes closing the connection, then do so here.
      if ((_commandBehavior & CommandBehavior.CloseConnection) != 0)
        _command.Connection.Close();

      _command = null;
    }

    /// <summary>
    /// Disposes the datareader.  Calls Close() to ensure everything is cleaned up.
    /// </summary>
    public override void Dispose()
    {
      Close();
      GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Throw an error if the datareader is closed
    /// </summary>
    private void CheckClosed()
    {
      if (_command == null)
        throw new InvalidOperationException("DataReader has been closed");
    }

    /// <summary>
    /// Enumerator support
    /// </summary>
    /// <returns>Returns a DbEnumerator object.</returns>
    public override Collections.IEnumerator GetEnumerator()
    {
      return new DbEnumerator(this);
    }

    /// <summary>
    /// 
    /// </summary>
    public override int Depth
    {
      get
      {
        CheckClosed();
        return 0;
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override int FieldCount
    {
      get
      {
        CheckClosed();
        return _fieldCount;
      }
    }

    /// <summary>
    /// SQLite is inherently un-typed.  All datatypes in SQLite are natively strings.  The definition of the columns of a table
    /// and the affinity of returned types are all we have to go on to type-restrict data in the reader.
    /// 
    /// This function attempts to verify that the type of data being requested of a column matches the datatype of the column.  In
    /// the case of columns that are not backed into a table definition, we attempt to match up the affinity of a column (int, double, string or blob)
    /// to a set of known types that closely match that affinity.  It's not an exact science, but its the best we can do.
    /// </summary>
    /// <returns>
    /// This function throws an InvalidTypeCast() exception if the requested type doesn't match the column's definition or affinity.
    /// </returns>
    /// <param name="ordinal">The index of the column to type-check</param>
    /// <param name="typ">The type we want to get out of the column</param>
    private void VerifyType(int ordinal, DbType typ)
    {
      SQLiteType t = GetSQLiteType(ordinal);

      if (t.Type == typ) return;

      if (t.Type != DbType.Object)
      {
        // Coercable type, usually a literal of some kind
        switch (_fieldTypeArray[ordinal].Affinity)
        {
          case TypeAffinity.Int64:
            if (typ == DbType.Int16) return;
            if (typ == DbType.Int32) return;
            if (typ == DbType.Int64) return;
            if (typ == DbType.Boolean) return;
            if (typ == DbType.Byte) return;
            break;
          case TypeAffinity.Double:
            if (typ == DbType.Single) return;
            if (typ == DbType.Double) return;
            if (typ == DbType.Decimal) return;
            break;
          case TypeAffinity.Text:
            if (typ == DbType.SByte) return;
            if (typ == DbType.String) return;
            if (typ == DbType.SByte) return;
            if (typ == DbType.Guid) return;
            if (typ == DbType.DateTime) return;
            break;
          case TypeAffinity.Blob:
            if (typ == DbType.String) return;
            if (typ == DbType.Binary) return;
            break;
        }
      }

      throw new InvalidCastException();
    }

    /// <summary>
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override bool GetBoolean(int ordinal)
    {
      VerifyType(ordinal, DbType.Boolean);
      return Convert.ToBoolean(GetValue(ordinal));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override byte GetByte(int ordinal)
    {
      VerifyType(ordinal, DbType.Byte);
      return Convert.ToByte(_activeStatement._sql.GetInt32(_activeStatement, ordinal));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <param name="dataOffset"></param>
    /// <param name="buffer"></param>
    /// <param name="bufferOffset"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
    {
      VerifyType(ordinal, DbType.Binary);
      return _activeStatement._sql.GetBytes(_activeStatement, ordinal, (int)dataOffset, buffer, bufferOffset, length);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override char GetChar(int ordinal)
    {
      VerifyType(ordinal, DbType.SByte);
      return Convert.ToChar(_activeStatement._sql.GetInt32(_activeStatement, ordinal));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <param name="dataOffset"></param>
    /// <param name="buffer"></param>
    /// <param name="bufferOffset"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
    {
      VerifyType(ordinal, DbType.String);
      return _activeStatement._sql.GetChars(_activeStatement, ordinal, (int)dataOffset, buffer, bufferOffset, length);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override string GetDataTypeName(int ordinal)
    {
      CheckClosed();
      return _activeStatement._sql.ColumnName(_activeStatement, ordinal);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override DateTime GetDateTime(int ordinal)
    {
      VerifyType(ordinal, DbType.DateTime);
      return _activeStatement._sql.GetDateTime(_activeStatement, ordinal);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override decimal GetDecimal(int ordinal)
    {
      VerifyType(ordinal, DbType.Decimal);
      return Convert.ToDecimal(_activeStatement._sql.GetDouble(_activeStatement, ordinal));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override double GetDouble(int ordinal)
    {
      VerifyType(ordinal, DbType.Double);
      return _activeStatement._sql.GetDouble(_activeStatement, ordinal);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override Type GetFieldType(int ordinal)
    {
      SQLiteType t = GetSQLiteType(ordinal);

      if (t.Type != DbType.Object)
        return SQLiteConvert.DbTypeToType(t.Type);

      switch (t.Affinity)
      {
        case TypeAffinity.Null:
          return typeof(DBNull);
        case TypeAffinity.Int64:
          return typeof(Int64);
        case TypeAffinity.Double:
          return typeof(Double);
        case TypeAffinity.Blob:
          return typeof(byte[]);
        default:
          return typeof(string);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override float GetFloat(int ordinal)
    {
      VerifyType(ordinal, DbType.Single);
      return Convert.ToSingle(_activeStatement._sql.GetDouble(_activeStatement, ordinal));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override Guid GetGuid(int ordinal)
    {
      VerifyType(ordinal, DbType.Guid);
      return new Guid(_activeStatement._sql.GetText(_activeStatement, ordinal));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override Int16 GetInt16(int ordinal)
    {
      VerifyType(ordinal, DbType.Int16);
      return Convert.ToInt16(_activeStatement._sql.GetInt32(_activeStatement, ordinal));
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override Int32 GetInt32(int ordinal)
    {
      VerifyType(ordinal, DbType.Int32);
      return _activeStatement._sql.GetInt32(_activeStatement, ordinal);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override Int64 GetInt64(int ordinal)
    {
      VerifyType(ordinal, DbType.Int64);
      return _activeStatement._sql.GetInt64(_activeStatement, ordinal);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override string GetName(int ordinal)
    {
      CheckClosed();
      return _activeStatement._sql.ColumnName(_activeStatement, ordinal);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public override int GetOrdinal(string name)
    {
      CheckClosed();
      return _activeStatement._sql.ColumnIndex(_activeStatement, name);
    }

    /// <summary>
    /// Schema information in SQLite is an iffy-business.  We've extended the native SQLite3.DLL to include a special pragma called
    /// PRAGMA real_column_names
    /// When enabled, the pragma causes all column aliases to be ignored, and the full Database.Table.ColumnName to be returned for
    /// each column of a SELECT statement.  Using this information it is then possible to query each database and table for the
    /// matching column, and associate it with the active statement.
    /// </summary>
    /// <remarks>
    /// The current connection is cloned for the sake of executing this statement, so as to avoid any possibility of corrupting the
    /// original connection's existing statements or state.  Any attached databases are re-attached to the new connection.
    /// </remarks>
    /// <returns>Returns a DataTable containing the schema information for the active SELECT statement being processed.</returns>
    public override DataTable GetSchemaTable()
    {
      CheckClosed();

      DataTable tbl = new DataTable("Schema");
      string[] arName;
      string strTable;
      string strCatalog;
      DataRow row;

      tbl.Columns.Add(SchemaTableColumn.ColumnName, typeof(String));
      tbl.Columns.Add(SchemaTableColumn.ColumnOrdinal, typeof(Int32));
      tbl.Columns.Add(SchemaTableColumn.ColumnSize, typeof(Int32));
      tbl.Columns.Add(SchemaTableColumn.NumericPrecision, typeof(Int32));
      tbl.Columns.Add(SchemaTableColumn.NumericScale, typeof(Int32));
      tbl.Columns.Add(SchemaTableColumn.DataType, typeof(Type));
      tbl.Columns.Add(SchemaTableColumn.ProviderType, typeof(Int32));
      tbl.Columns.Add(SchemaTableColumn.IsLong, typeof(Boolean));
      tbl.Columns.Add(SchemaTableColumn.AllowDBNull, typeof(Boolean));
      tbl.Columns.Add(SchemaTableOptionalColumn.IsReadOnly, typeof(Boolean));
      tbl.Columns.Add(SchemaTableOptionalColumn.IsRowVersion, typeof(Boolean));
      tbl.Columns.Add(SchemaTableColumn.IsUnique, typeof(Boolean));
      tbl.Columns.Add(SchemaTableColumn.IsKey, typeof(Boolean));
      tbl.Columns.Add(SchemaTableOptionalColumn.IsAutoIncrement, typeof(Boolean));
      tbl.Columns.Add(SchemaTableColumn.BaseSchemaName, typeof(String));
      tbl.Columns.Add(SchemaTableOptionalColumn.BaseCatalogName, typeof(String));
      tbl.Columns.Add(SchemaTableColumn.BaseTableName, typeof(String));
      tbl.Columns.Add(SchemaTableColumn.BaseColumnName, typeof(String));
      tbl.Columns.Add(SchemaTableOptionalColumn.BaseColumnNamespace, typeof(string));
      tbl.Columns.Add(SchemaTableOptionalColumn.DefaultValue, typeof(object));

      tbl.BeginLoadData();

      SQLiteConnection cnn = (SQLiteConnection)_command.Connection;

      try
      {
        cnn._sql.SetRealColNames(true);

        // Create a new command based on the original.  The only difference being that this new command returns
        // fully-qualified Database.Table.Column column names because of the above pragma
        using (SQLiteCommand cmd = new SQLiteCommand(_activeStatement._sqlStatement, cnn))
        {
          using (DbDataReader rd = cmd.ExecuteReader())
          {
            // No need to Read() from this reader, we just want the column names
            for (int n = 0; n < FieldCount; n++)
            {
              strTable = "";
              strCatalog = "main";

              row = tbl.NewRow();

              // Default settings for the column
              row[SchemaTableColumn.ColumnName] = GetName(n);
              row[SchemaTableColumn.ColumnOrdinal] = n;
              row[SchemaTableColumn.ColumnSize] = 0;
              row[SchemaTableColumn.NumericPrecision] = 0;
              row[SchemaTableColumn.NumericScale] = 0;
              row[SchemaTableColumn.DataType] = GetFieldType(n);
              row[SchemaTableColumn.ProviderType] = GetSQLiteType(n).Type;
              row[SchemaTableColumn.IsLong] = false;
              row[SchemaTableColumn.AllowDBNull] = true;
              row[SchemaTableOptionalColumn.IsReadOnly] = true;
              row[SchemaTableOptionalColumn.IsRowVersion] = false;
              row[SchemaTableColumn.IsUnique] = false;
              row[SchemaTableColumn.IsKey] = false;
              row[SchemaTableOptionalColumn.IsAutoIncrement] = false;
              row[SchemaTableOptionalColumn.IsReadOnly] = false;
              row[SchemaTableColumn.BaseColumnName] = GetName(n);

              // Try and extract the database, table and column from the datareader
              arName = rd.GetName(n).Split('.');

              if (arName.Length > 1)
                strTable = arName[arName.Length - 2];

              if (arName.Length > 2)
                strCatalog = arName[arName.Length - 3];

              // If we have a table-bound column, extract the extra information from it
              if (arName.Length > 1)
              {
                using (SQLiteCommand cmdTable = new SQLiteCommand(String.Format("PRAGMA [{1}].TABLE_INFO([{0}])", strTable, strCatalog), cnn))
                {
                  if (arName.Length < 3) strCatalog = "";

                  using (DbDataReader rdTable = cmdTable.ExecuteReader())
                  {
                    while (rdTable.Read())
                    {
                      if (String.Compare(arName[arName.Length - 1], rdTable.GetString(1), true) == 0)
                      {
                        string strType = rdTable.GetString(2);
                        string[] arSize = strType.Split('(');
                        if (arSize.Length > 1)
                        {
                          arSize = arSize[1].Split(')');
                          if (arSize.Length > 1)
                            row["ColumnSize"] = Convert.ToInt32(arSize[0]);
                        }
                        bool bNotNull = rdTable.GetBoolean(3);
                        bool bPrimaryKey = rdTable.GetBoolean(5);

                        row[SchemaTableColumn.BaseTableName] = strTable;
                        row[SchemaTableColumn.BaseColumnName] = rdTable.GetString(1);
                        if (strCatalog.Length > 0)
                        {
                          row[SchemaTableOptionalColumn.BaseColumnNamespace] = strCatalog;
                          row[SchemaTableColumn.BaseSchemaName] = strCatalog;
                        }

                        row[SchemaTableColumn.AllowDBNull] = (!bNotNull && !bPrimaryKey);
                        row[SchemaTableColumn.IsUnique] = bPrimaryKey;
                        row[SchemaTableColumn.IsKey] = bPrimaryKey;
                        row[SchemaTableOptionalColumn.IsAutoIncrement] = (bPrimaryKey && String.Compare(strType, "Integer", true) == 0);
                        row[SchemaTableOptionalColumn.IsReadOnly] = !(bool)row[SchemaTableOptionalColumn.IsAutoIncrement];
                        if (rdTable.IsDBNull(4) == false)
                          row[SchemaTableOptionalColumn.DefaultValue] = rdTable[4];
                        break;
                      }
                    }
                  }
                }
              }
              tbl.Rows.Add(row);
            }
          }
        }
      }
      catch (Exception e)
      {
        throw (e);
      }
      finally
      {
        cnn._sql.SetRealColNames(false);
      }

      tbl.AcceptChanges();
      tbl.EndLoadData();

      return tbl;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override string GetString(int ordinal)
    {
      VerifyType(ordinal, DbType.String);
      return _activeStatement._sql.GetText(_activeStatement, ordinal);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override object GetValue(int ordinal)
    {
      if (IsDBNull(ordinal)) return DBNull.Value;

      if (GetFieldType(ordinal) == typeof(byte[]))
      {
        int n = (int)GetBytes(ordinal, 0, null, 0, 0);
        byte[] b = new byte[n];
        GetBytes(ordinal, 0, b, 0, n);

        return b;
      }

      return Convert.ChangeType(_activeStatement._sql.GetText(_activeStatement, ordinal), GetFieldType(ordinal), null);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="values"></param>
    /// <returns></returns>
    public override int GetValues(object[] values)
    {
      CheckClosed();
      int nMax = _fieldCount;
      if (values.Length < nMax) nMax = values.Length;

      for (int n = 0; n < nMax; n++)
      {
        values.SetValue(GetValue(n), n);
      }

      return nMax;
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool HasRows
    {
      get
      {
        CheckClosed();
        return (_readingState != 2);
      }
    }

    /// <summary>
    /// 
    /// </summary>
    public override bool IsClosed
    {
      get { return (_command == null); }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override bool IsDBNull(int ordinal)
    {
      CheckClosed();
      return _activeStatement._sql.IsNull(_activeStatement, ordinal);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override bool NextResult()
    {
      CheckClosed();

      SQLiteStatement stmt;
      int fieldCount;

      while (true)
      {
        if (_activeStatement != null)
        {
          // If we're only supposed to return a single rowset, step through all remaining statements once until
          // they are all done and return false to indicate no more resultsets exist.
          if ((_commandBehavior & CommandBehavior.SingleResult) != 0)
          {
            // Reset the previously-executed command
            _activeStatement._sql.Reset(_activeStatement);

            while (_activeStatementIndex + 1 != _command._statementList.Length)
            {
              _activeStatementIndex++;
              stmt = _command._statementList[_activeStatementIndex];
              stmt._sql.Step(stmt);
              stmt._sql.Reset(stmt); // Gotta reset after every step to release any locks and such!
            }
            return false;
          }

          // Reset the previously-executed command
          _activeStatement._sql.Reset(_activeStatement);
        }

        // If we've reached the end of the statements, return false, no more resultsets
        if (_activeStatementIndex + 1 == _command._statementList.Length)
          return false;

        // If we were on a resultset, set the state to "done reading" for it
        if (_readingState < 1)
          _readingState = 1;

        _activeStatementIndex++;

        stmt = _command._statementList[_activeStatementIndex];
        fieldCount = stmt._sql.ColumnCount(stmt);

        // If we're told to get schema information only, then don't perform an initial step() through the resultset
        if ((_commandBehavior & CommandBehavior.SchemaOnly) == 0 || fieldCount == 0)
        {
          if (stmt._sql.Step(stmt))
          {
            _readingState = -1;
          }
          else if (fieldCount == 0) // No rows returned, if fieldCount is zero, skip to the next statement
          {
            stmt._sql.Reset(stmt);
            continue; // Skip this command and move to the next, it was not a row-returning resultset
          }
          else // No rows, fieldCount is non-zero so stop here
          {
            _readingState = 1; // This command returned columns but no rows, so return true, but HasRows = false and Read() returns false
          }
        }

        // Ahh, we found a row-returning resultset eligible to be returned!
        _activeStatement = stmt;
        _fieldCount = fieldCount;
        _fieldTypeArray = null;

        return true;
      }
    }

    private SQLiteType GetSQLiteType(int ordinal)
    {
      CheckClosed();
      if (_fieldTypeArray == null) _fieldTypeArray = new SQLiteType[_fieldCount];

      if (_fieldTypeArray[ordinal].Affinity == 0)
        _fieldTypeArray[ordinal].Type = SQLiteConvert.TypeNameToDbType(_activeStatement._sql.ColumnType(_activeStatement, ordinal, out _fieldTypeArray[ordinal].Affinity));
      return _fieldTypeArray[ordinal];
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override bool Read()
    {
      CheckClosed();

      if (_readingState == -1) // First step was already done at the NextResult() level, so don't step again, just return true.
      {
        _readingState = 0;
        return true;
      }
      else if (_readingState == 0) // Actively reading rows
      {
        if (_activeStatement._sql.Step(_activeStatement) == true)
          return true;

        _readingState = 1; // Finished reading rows
      }

      return false;
    }

    /// <summary>
    /// 
    /// </summary>
    public override int RecordsAffected
    {
      get { return _rowsAffected; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public override object this[string name]
    {
      get { return GetValue(GetOrdinal(name)); }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="ordinal"></param>
    /// <returns></returns>
    public override object this[int ordinal]
    {
      get { return GetValue(ordinal); }
    }
  }
}
