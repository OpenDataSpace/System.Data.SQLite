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
  /// The I/O file cache flushing behavior for the connection
  /// </summary>
  public enum SyncMode
  {
    /// <summary>
    /// Normal file flushing at critical sections of the code
    /// </summary>
    Normal = 0,
    /// <summary>
    /// Full file flushing after every write operation
    /// </summary>
    Full = 1,
    /// <summary>
    /// Use the default operating system's file flushing, SQLite does not explicitly flush the file buffers after writing
    /// </summary>
    Off = 2,
  }

  /// <summary>
  /// SQLite implentation of DbConnection.
  /// </summary>
  /// <remarks>
  /// The <see cref="ConnectionString">ConnectionString</see> property of the SQLiteConnection class can contain the following parameter(s), delimited with a semi-colon:
  /// <list type="table">
  /// <listheader>
  /// <term>Parameter</term>
  /// <term>Values</term>
  /// <term>Required</term>
  /// <term>Default</term>
  /// </listheader>
  /// <item>
  /// <description>Data Source</description>
  /// <description>{filename}</description>
  /// <description>Y</description>
  /// <description></description>
  /// </item>
  /// <item>
  /// <description>Version</description>
  /// <description>3</description>
  /// <description>N</description>
  /// <description>3</description>
  /// </item>
  /// <item>
  /// <description>UseUTF16Encoding</description>
  /// <description><b>True</b><br/><b>False</b></description>
  /// <description>N</description>
  /// <description>False</description>
  /// </item>
  /// <item>
  /// <description>DateTimeFormat</description>
  /// <description><b>Ticks</b> - Use DateTime.Ticks<br/><b>ISO8601</b> - Use ISO8601 DateTime format</description>
  /// <description>N</description>
  /// <description>ISO8601</description>
  /// </item>
  /// <item>
  /// <description>Cache Size</description>
  /// <description>{size in bytes}</description>
  /// <description>N</description>
  /// <description>2000</description>
  /// </item>
  /// <item>
  /// <description>Synchronous</description>
  /// <description><b>Normal</b> - Normal file flushing behavior<br/><b>Full</b> - Full flushing after all writes<br/><b>Off</b> - Underlying OS flushes I/O's</description>
  /// <description>N</description>
  /// <description>Normal</description>
  /// </item>
  /// <item>
  /// <description>Page Size</description>
  /// <description>{size in bytes}</description>
  /// <description>N</description>
  /// <description>1024</description>
  /// </item>
  /// </list>
  /// </remarks>
  public sealed class SQLiteConnection : DbConnection, ICloneable
  {
    /// <summary>
    /// State of the current connection
    /// </summary>
    private ConnectionState     _connectionState;
    /// <summary>
    /// The connection string
    /// </summary>
    private string              _connectionString;
    /// <summary>
    /// One transaction allowed per connection please!
    /// </summary>
    internal DbTransaction       _activeTransaction;
    /// <summary>
    /// The base SQLite object to interop with
    /// </summary>
    internal SQLiteBase          _sql;
    /// <summary>
    /// Commands associated with this connection
    /// </summary>
    internal List<SQLiteCommand> _commandList;

    /// <event/>
    /// <summary>
    /// This event is raised whenever the database is opened or closed.
    /// </summary>
    public override event StateChangeEventHandler StateChange;

    ///<overloads>
    /// Constructs a new SQLiteConnection object
    /// </overloads>
    /// <summary>
    /// Default constructor
    /// </summary>
    public SQLiteConnection()
    {
      Initialize(null);
    }

    /// <summary>
    /// Initializes the connection with the specified connection string
    /// </summary>
    /// <param name="connectionString">The connection string to use on the connection</param>
    public SQLiteConnection(string connectionString)
    {
      Initialize(connectionString);
    }

    /// <summary>
    /// Clones the settings and connection string from an existing connection.  If the existing connection is already open, this
    /// function will open its own connection, enumerate any attached databases of the original connection, and automatically
    /// attach to them.
    /// </summary>
    /// <param name="cnn"></param>
    public SQLiteConnection(SQLiteConnection cnn)
    {
      string str;

      Initialize(cnn.ConnectionString);

      if (cnn.State == ConnectionState.Open)
      {
        Open();

        // Reattach all attached databases from the existing connection
        using (DataTable tbl = cnn.GetSchema("Catalogs"))
        {
          foreach (DataRow row in tbl.Rows)
          {
            str = row[0].ToString();
            if (String.Compare(str, "MAIN", true) != 0 && String.Compare(str, "TEMP", true) != 0)
            {
              _sql.Execute(String.Format("ATTACH DATABASE '{0}' AS [{1}]", row[1], row[0]));
            }
          }
        }
      }
    }

#if PLATFORM_COMPACTFRAMEWORK
    /// <summary>
    /// Obsolete
    /// </summary>
    public override int ConnectionTimeout
    {
      get
      {
        return 30;
      }
    }
#endif

    /// <summary>
    /// Creates a clone of the connection.  All attached databases and user-defined functions are cloned.  If the existing connection is open, the cloned connection 
    /// will also be opened.
    /// </summary>
    /// <returns></returns>
    public object Clone()
    {
      return new SQLiteConnection(this);
    }

    private void Initialize(string connectionString)
    {
      _sql = null;
      _connectionState = ConnectionState.Closed;
      _connectionString = "";
      _activeTransaction = null;
      _commandList = new List<SQLiteCommand>();

      if (connectionString != null)
        ConnectionString = connectionString;
    }

    /// <summary>
    /// Disposes of the SQLiteConnection, closing it if it is active.
    /// </summary>
    /// <param name="bDisposing">True if the connection is being explicitly closed.</param>
    protected override void Dispose(bool bDisposing)
    {
      base.Dispose(bDisposing);
      Close();
    }

    /// <summary>
    /// Raises the state change event when the state of the connection changes
    /// </summary>
    /// <param name="newState">The new state.  If it is different from the previous state, an event is raised.</param>
    internal void OnStateChange(ConnectionState newState)
    {
      ConnectionState oldState = _connectionState;
      _connectionState = newState;

      if (StateChange != null && oldState != newState)
      {
        StateChangeEventArgs e = new StateChangeEventArgs(oldState, newState);
        StateChange(this, e);
      }
    }

    /// <summary>
    /// Creates a new SQLiteTransaction if one isn't already active on the connection.
    /// </summary>
    /// <param name="isolationLevel">SQLite doesn't support varying isolation levels, so this parameter is ignored.</param>
    /// <returns>Returns a SQLiteTransaction object.</returns>
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
      if (_connectionState != ConnectionState.Open)
        throw new InvalidOperationException();

      if (_activeTransaction != null)
        throw new ArgumentException("Transaction already pending");

      _activeTransaction = new SQLiteTransaction(this);
      return _activeTransaction;
    }

    /// <summary>
    /// Not implemented
    /// </summary>
    /// <param name="databaseName"></param>
    public override void ChangeDatabase(string databaseName)
    {
      throw new NotImplementedException();
    }

    /// <summary>
    /// When the database connection is closed, all commands linked to this connection are automatically reset.
    /// </summary>
    public override void Close()
    {
      if (_sql != null)
      {
        int x = _commandList.Count;
        for (int n = 0; n < x; n++)
        {
          _commandList[n].ClearCommands();
        }
        _sql.Close();
      }

      _sql = null;

      OnStateChange(ConnectionState.Closed);
    }

    /// <summary>
    /// The connection string containing the parameters for the connection
    /// </summary>
    /// <remarks>
    /// <list type="table">
    /// <listheader>
    /// <term>Parameter</term>
    /// <term>Values</term>
    /// <term>Required</term>
    /// <term>Default</term>
    /// </listheader>
    /// <item>
    /// <description>Data Source</description>
    /// <description>{filename}</description>
    /// <description>Y</description>
    /// <description></description>
    /// </item>
    /// <item>
    /// <description>Version</description>
    /// <description>3</description>
    /// <description>N</description>
    /// <description>3</description>
    /// </item>
    /// <item>
    /// <description>UseUTF16Encoding</description>
    /// <description><b>True</b><br/><b>False</b></description>
    /// <description>N</description>
    /// <description>False</description>
    /// </item>
    /// <item>
    /// <description>DateTimeFormat</description>
    /// <description><b>Ticks</b> - Use DateTime.Ticks<br/><b>ISO8601</b> - Use ISO8601 DateTime format</description>
    /// <description>N</description>
    /// <description>ISO8601</description>
    /// </item>
    /// <item>
    /// <description>Cache Size</description>
    /// <description>{size in bytes}</description>
    /// <description>N</description>
    /// <description>2000</description>
    /// </item>
    /// <item>
    /// <description>Synchronous</description>
    /// <description><b>Normal</b> - Normal file flushing behavior<br/><b>Full</b> - Full flushing after all writes<br/><b>Off</b> - Underlying OS flushes I/O's</description>
    /// <description>N</description>
    /// <description>Normal</description>
    /// </item>
    /// <item>
    /// <description>Page Size</description>
    /// <description>{size in bytes}</description>
    /// <description>N</description>
    /// <description>4096</description>
    /// </item>
    /// </list>
    /// </remarks>
    public override string ConnectionString
    {
      get
      {
        return _connectionString;
      }
      set
      {
        if (value == null)
          throw new ArgumentNullException();

        else if (_connectionState != ConnectionState.Closed)
          throw new InvalidOperationException();

        _connectionString = value;
      }
    }

    /// <summary>
    /// Create a new SQLiteCommand and associate it with this connection.
    /// </summary>
    /// <returns>Returns an instantiated SQLiteCommand object already assigned to this connection.</returns>
    protected override DbCommand CreateDbCommand()
    {
      return new SQLiteCommand(this);
    }

    /// <summary>
    /// Not implemented.  Returns null.
    /// </summary>
    public override string DataSource
    {
      get { return null; }
    }

    /// <summary>
    /// Not implemented.  Returns null.
    /// </summary>
    public override string Database
    {
      get { return null; }
    }

    /// <summary>
    /// Parses the connection string into component parts
    /// </summary>
    /// <returns>An array of key-value pairs representing each parameter of the connection string</returns>
    internal KeyValuePair<string, string>[] ParseConnectionString()
    {
      string s = _connectionString;
      int n;
      KeyValuePair<string, string> kv;
      List<KeyValuePair<string, string>> ls = new List<KeyValuePair<string, string>>();

      // First split into semi-colon delimited values.  The Split() function of SQLiteBase accounts for and properly
      // skips semi-colons in quoted strings
      string[] arParts = SQLiteConvert.Split(s, ';');
      string[] arPiece;

      int x = arParts.Length;
      // For each semi-colon piece, split into key and value pairs by the presence of the = sign
      for (n = 0; n < x; n++)
      {
        arPiece = SQLiteConvert.Split(arParts[n], '=');
        if (arPiece.Length == 2)
        {
          kv.Key = arPiece[0];
          kv.Value = arPiece[1];
          ls.Add(kv);
        }
      }
      KeyValuePair<string, string>[] ar = new KeyValuePair<string, string>[ls.Count];
      ls.CopyTo(ar, 0);

      // Return the array of key-value pairs
      return ar;
    }

    /// <summary>
    /// Looks for a key in the array of key/values of the parameter string.  If not found, return the specified default value
    /// </summary>
    /// <param name="opts">The Key/Value pair array to look in</param>
    /// <param name="key">The key to find</param>
    /// <param name="defValue">The default value to return if the key is not found</param>
    /// <returns>The value corresponding to the specified key, or the default value if not found.</returns>
    internal string FindKey(KeyValuePair<string, string>[] opts, string key, string defValue)
    {
      int x = opts.Length;
      for (int n = 0; n < x; n++)
      {
        if (String.Compare(opts[n].Key, key, true) == 0)
        {
          return opts[n].Value;
        }
      }
      return defValue;
    }

    /// <summary>
    /// Opens the connection using the parameters found in the <see cref="ConnectionString">ConnectionString</see>
    /// </summary>
    public override void Open()
    {
      if (_connectionState != ConnectionState.Closed)
        throw new InvalidOperationException();

      Close();

      KeyValuePair<string, string>[] opts = ParseConnectionString();

      if (Convert.ToInt32(FindKey(opts, "Version", "3")) != 3)
        throw new NotImplementedException("Only SQLite Version 3 is supported at this time");

      try
      {
        string strFile = FindKey(opts, "Data Source", "");
        bool bUTF16 = (Convert.ToBoolean(FindKey(opts, "UseUTF16Encoding", "False")) == true);

        if (bUTF16)
          _sql = new SQLite3_UTF16(String.Compare(FindKey(opts, "DateTimeFormat", "ISO8601"), "TICKS") == 0 ? DateTimeFormat.Ticks : DateTimeFormat.ISO8601);
        else
          _sql = new SQLite3(String.Compare(FindKey(opts, "DateTimeFormat", "ISO8601"), "TICKS") == 0 ? DateTimeFormat.Ticks : DateTimeFormat.ISO8601);

        _sql.Open(strFile);

        if (bUTF16 == true)
          _sql.Execute("PRAGMA encoding = 'UTF-16'");
        else
          _sql.Execute("PRAGMA encoding = 'UTF-8'");

        _sql.Execute(String.Format("PRAGMA Synchronous={0}", FindKey(opts, "Synchronous", "Normal")));
        _sql.Execute(String.Format("PRAGMA Cache_Size={0}", FindKey(opts, "Cache Size", "2000")));
        if (String.Compare(strFile, ":MEMORY:", true) != 0)
          _sql.Execute(String.Format("PRAGMA Page_Size={0}", FindKey(opts, "Page Size", "1024")));
      }
      catch (SQLiteException e)
      {
        OnStateChange(ConnectionState.Broken);
        throw (e);
      }
      OnStateChange(ConnectionState.Open);
    }

    /// <summary>
    /// Returns the version of the underlying SQLite database engine
    /// </summary>
    public override string ServerVersion
    {
      get
      {
        if (_connectionState != ConnectionState.Open)
          throw new InvalidOperationException();

        return _sql.Version;
      }
    }

    /// <summary>
    /// Returns the state of the connection.
    /// </summary>
    public override ConnectionState State
    {
      get { return _connectionState; }
    }

    ///<overloads>
    /// The following commands are used to extract schema information out of the database.  Valid schema types are:
    /// <list type="bullet">
    /// <item>
    /// <description>MetaDataCollections</description>
    /// </item>
    /// <item>
    /// <description>DataSourceInformation</description>
    /// </item>
    /// <item>
    /// <description>Columns</description>
    /// </item>
    /// <item>
    /// <description>Indexes</description>
    /// </item>
    /// <item>
    /// <description>Tables</description>
    /// </item>
    /// <item>
    /// <description>Views</description>
    /// </item>
    /// <item>
    /// <description>Catalogs</description>
    /// </item>
    /// </list>
    /// </overloads>
    /// <summary>
    /// Returns the MetaDataCollections schema
    /// </summary>
    /// <returns>A DataTable of the MetaDataCollections schema</returns>
    public override DataTable GetSchema()
    {
      return GetSchema("MetaDataCollections", null);
    }

    /// <summary>
    /// Returns schema information of the specified collection
    /// </summary>
    /// <param name="collectionName">The schema collection to retrieve</param>
    /// <returns>A DataTable of the specified collection</returns>
    public override DataTable GetSchema(string collectionName)
    {
      return GetSchema(collectionName, new string[0]);
    }

    /// <summary>
    /// Retrieves schema information using the specified constraint(s) for the specified collection
    /// </summary>
    /// <param name="collectionName">The collection to retrieve</param>
    /// <param name="restrictionValues">The restrictions to impose</param>
    /// <returns>A DataTable of the specified collection</returns>
    public override DataTable GetSchema(string collectionName, string[] restrictionValues)
    {
      if (_connectionState != ConnectionState.Open)
        throw new InvalidOperationException();

      string[] parms = new string[5];

      restrictionValues.CopyTo(parms, 0);

      if (restrictionValues == null) restrictionValues = new string[0];
      switch (collectionName.ToUpper())
      {
        case "METADATACOLLECTIONS":
          return Schema_MetaDataCollections();
        case "DATASOURCEINFORMATION":
          return Schema_DataSourceInformation();
        case "COLUMNS":
          return Schema_Columns(parms[0], parms[2], parms[3]);
        case "INDEXES":
          return Schema_Indexes(parms[0], parms[2], parms[4]);
        case "TABLES":
          return Schema_Tables(parms[0], parms[2], parms[3]);
        case "VIEWS":
          return Schema_Views(parms[0], parms[2]);
        case "CATALOGS":
          return Schema_Catalogs(parms[0]);
      }
      return null;
    }

    /// <summary>
    /// Builds a MetaDataCollections schema datatable
    /// </summary>
    /// <returns>DataTable</returns>
    private DataTable Schema_MetaDataCollections()
    {
      DataTable tbl = new DataTable("MetaDataCollections");
      DataRow row;

      tbl.Columns.Add("CollectionName", typeof(string));
      tbl.Columns.Add("NumberOfRestrictions", typeof(int));
      tbl.Columns.Add("NumberOfIdentifierParts", typeof(int));

      tbl.BeginLoadData();

      row = tbl.NewRow();
      row.ItemArray = new object[] { "MetaDataCollections", 0, 0 };
      tbl.Rows.Add(row);

      row = tbl.NewRow();
      row.ItemArray = new object[] { "DataSourceInformation", 0, 0 };
      tbl.Rows.Add(row);

      row = tbl.NewRow();
      row.ItemArray = new object[] { "Catalogs", 1, 1 };
      tbl.Rows.Add(row);

      row = tbl.NewRow();
      row.ItemArray = new object[] { "Columns", 4, 4 };
      tbl.Rows.Add(row);

      row = tbl.NewRow();
      row.ItemArray = new object[] { "Indexes", 5, 4 };
      tbl.Rows.Add(row);

      row = tbl.NewRow();
      row.ItemArray = new object[] { "Tables", 4, 3 };
      tbl.Rows.Add(row);

      row = tbl.NewRow();
      row.ItemArray = new object[] { "Views", 3, 3 };
      tbl.Rows.Add(row);

      tbl.AcceptChanges();
      tbl.EndLoadData();

      return tbl;
    }

    /// <summary>
    /// Builds a DataSourceInformation datatable
    /// </summary>
    /// <returns>DataTable</returns>
    private DataTable Schema_DataSourceInformation()
    {
      DataTable tbl = new DataTable("DataSourceInformation");
      DataRow row;

      tbl.Columns.Add("CompositeIdentifierSeparatorPattern", typeof(string));
      tbl.Columns.Add("DataSourceProductName", typeof(string));
      tbl.Columns.Add("DataSourceProductVersion", typeof(string));
      tbl.Columns.Add("DataSourceProductVersionNormalized", typeof(string));
      tbl.Columns.Add("GroupByBehavior", typeof(int));
      tbl.Columns.Add("IdentifierPattern", typeof(string));
      tbl.Columns.Add("IdentifierCase", typeof(int));
      tbl.Columns.Add("OrderByColumnsInSelect", typeof(bool));
      tbl.Columns.Add("ParameterMarkerFormat", typeof(string));
      tbl.Columns.Add("ParameterMarkerPattern", typeof(string));
      tbl.Columns.Add("ParameterNameMaxLength", typeof(int));
      tbl.Columns.Add("ParameterNamePattern", typeof(string));
      tbl.Columns.Add("QuotedIdentifierPattern", typeof(string));
      tbl.Columns.Add("QuotedIdentifierCase", typeof(int));
      tbl.Columns.Add("StatementSeparatorPattern", typeof(string));
      tbl.Columns.Add("StringLiteralPattern", typeof(string));
      tbl.Columns.Add("SupportedJoinOperators", typeof(int));

      tbl.BeginLoadData();

      // TODO: Fixup the regular expressions to support only the SQLite stuff, they were originally cloned
      // from JET's DataSourceInformation return result.
      row = tbl.NewRow();
      row.ItemArray = new object[] {
        DBNull.Value,
        "SQLite",
        _sql.Version,
        _sql.Version,
        3,
        @"[^ ][^\.!`\[\]]*",
        1,
        DBNull.Value,
        "?",
        "?",
        0,
        DBNull.Value,
        @"`(([^`]|``)*)`",
        1,
        DBNull.Value,
        DBNull.Value,
        DBNull.Value
      };
      tbl.Rows.Add(row);

      tbl.AcceptChanges();
      tbl.EndLoadData();

      return tbl;
    }

    /// <summary>
    /// Build a Columns schema
    /// </summary>
    /// <param name="strCatalog">The catalog (attached database) to query, can be null</param>
    /// <param name="strTable">The table to retrieve schema information for, must not be null</param>
    /// <param name="strColumn">The column to retrieve schema information for, can be null</param>
    /// <returns>DataTable</returns>
    private DataTable Schema_Columns(string strCatalog, string strTable, string strColumn)
    {
      DataTable tbl = new DataTable("Columns");
      DataRow row;

      tbl.Columns.Add("TABLE_CATALOG", typeof(string));
      tbl.Columns.Add("TABLE_SCHEMA", typeof(string));
      tbl.Columns.Add("TABLE_NAME", typeof(string));
      tbl.Columns.Add("COLUMN_NAME", typeof(string));
      tbl.Columns.Add("COLUMN_GUID", typeof(Guid));
      tbl.Columns.Add("COLUMN_PROPID", typeof(long));
      tbl.Columns.Add("ORDINAL_POSITION", typeof(long));
      tbl.Columns.Add("COLUMN_HASDEFAULT", typeof(bool));
      tbl.Columns.Add("COLUMN_DEFAULT", typeof(string));
      tbl.Columns.Add("COLUMN_FLAGS", typeof(long));
      tbl.Columns.Add("IS_NULLABLE", typeof(bool));
      tbl.Columns.Add("DATA_TYPE", typeof(int));
      tbl.Columns.Add("TYPE_GUID", typeof(Guid));
      tbl.Columns.Add("CHARACTER_MAXIMUM_LENGTH", typeof(long));
      tbl.Columns.Add("CHARACTER_OCTET_LENGTH", typeof(long));
      tbl.Columns.Add("NUMERIC_PRECISION", typeof(int));
      tbl.Columns.Add("NUMERIC_SCALE", typeof(short));
      tbl.Columns.Add("DATETIME_PRECISION", typeof(long));
      tbl.Columns.Add("CHARACTER_SET_CATALOG", typeof(string));
      tbl.Columns.Add("CHARACTER_SET_SCHEMA", typeof(string));
      tbl.Columns.Add("CHARACTER_SET_NAME", typeof(string));
      tbl.Columns.Add("COLLATION_CATALOG", typeof(string));
      tbl.Columns.Add("COLLATION_SCHEMA", typeof(string));
      tbl.Columns.Add("COLLATION_NAME", typeof(string));
      tbl.Columns.Add("DOMAIN_CATALOG", typeof(string));
      tbl.Columns.Add("DOMAIN_NAME", typeof(string));
      tbl.Columns.Add("DESCRIPTION", typeof(string));

      tbl.BeginLoadData();

      if (strCatalog == null || strCatalog == "") strCatalog = "main";

      using (SQLiteCommand cmd = new SQLiteCommand(String.Format("SELECT * FROM [{0}].[{1}]", strCatalog, strTable), this))
      {
        using (SQLiteDataReader rd = (SQLiteDataReader)cmd.ExecuteReader(CommandBehavior.SchemaOnly))
        {
          using (DataTable tblSchema = rd.GetSchemaTable())
          {
            foreach (DataRow schemaRow in tblSchema.Rows)
            {
              if (String.Compare(schemaRow[SchemaTableColumn.ColumnName].ToString(), strColumn, true) == 0 || strColumn == null)
              {
                row = tbl.NewRow();

                row["TABLE_NAME"] = strTable;
                row["COLUMN_NAME"] = schemaRow[SchemaTableColumn.ColumnName];
                row["TABLE_CATALOG"] = schemaRow[SchemaTableOptionalColumn.BaseCatalogName];
                row["ORDINAL_POSITION"] = schemaRow[SchemaTableColumn.ColumnOrdinal];
                row["COLUMN_HASDEFAULT"] = (schemaRow[SchemaTableOptionalColumn.DefaultValue] != DBNull.Value);
                row["COLUMN_DEFAULT"] = schemaRow[SchemaTableOptionalColumn.DefaultValue];
                row["IS_NULLABLE"] = schemaRow[SchemaTableColumn.AllowDBNull];
                row["DATA_TYPE"] = schemaRow[SchemaTableColumn.ProviderType];
                row["CHARACTER_MAXIMUM_LENGTH"] = schemaRow[SchemaTableColumn.ColumnSize];

                tbl.Rows.Add(row);
              }
            }
          }
        }
      }

      tbl.AcceptChanges();
      tbl.EndLoadData();

      return tbl;
    }

    /// <summary>
    /// Returns index information for the given database and catalog
    /// </summary>
    /// <param name="strCatalog">The catalog (attached database) to query, can be null</param>
    /// <param name="strIndex">The name of the index to retrieve information for, can be null</param>
    /// <param name="strTable">The table to retrieve index information for, can be null</param>
    /// <returns>DataTable</returns>
    private DataTable Schema_Indexes(string strCatalog, string strIndex, string strTable)
    {
      DataTable tbl = new DataTable("Indexes");
      DataRow row;

      tbl.Columns.Add("TABLE_CATALOG", typeof(string));
      tbl.Columns.Add("TABLE_SCHEMA", typeof(string));
      tbl.Columns.Add("TABLE_NAME", typeof(string));
      tbl.Columns.Add("INDEX_CATALOG", typeof(string));
      tbl.Columns.Add("INDEX_SCHEMA", typeof(string));
      tbl.Columns.Add("INDEX_NAME", typeof(string));
      tbl.Columns.Add("PRIMARY_KEY", typeof(bool));
      tbl.Columns.Add("UNIQUE", typeof(bool));
      tbl.Columns.Add("CLUSTERED", typeof(bool));
      tbl.Columns.Add("TYPE", typeof(int));
      tbl.Columns.Add("FILL_FACTOR", typeof(int));
      tbl.Columns.Add("INITIAL_SIZE", typeof(int));
      tbl.Columns.Add("NULLS", typeof(int));
      tbl.Columns.Add("SORT_BOOKMARKS", typeof(bool));
      tbl.Columns.Add("AUTO_UPDATE", typeof(bool));
      tbl.Columns.Add("NULL_COLLATION", typeof(int));
      tbl.Columns.Add("ORDINAL_POSITION", typeof(long));
      tbl.Columns.Add("COLUMN_NAME", typeof(string));
      tbl.Columns.Add("COLUMN_GUID", typeof(Guid));
      tbl.Columns.Add("COLUMN_PROPID", typeof(long));
      tbl.Columns.Add("COLLATION", typeof(short));
      tbl.Columns.Add("CARDINALITY", typeof(Decimal));
      tbl.Columns.Add("PAGES", typeof(int));
      tbl.Columns.Add("FILTER_CONDITION", typeof(string));
      tbl.Columns.Add("INTEGRATED", typeof(bool));

      tbl.BeginLoadData();

      if (strCatalog == null || strCatalog == "") strCatalog = "main";

      using (SQLiteCommand cmd = new SQLiteCommand(String.Format("SELECT * FROM [{0}].[sqlite_master] WHERE [type] = 'index'", strCatalog), this))
      {
        using (SQLiteDataReader rd = (SQLiteDataReader)cmd.ExecuteReader())
        {
          while (rd.Read())
          {
            if (String.Compare(rd.GetString(1), strIndex, true) == 0 || strIndex == null)
            {
              if (String.Compare(rd.GetString(2), strTable, true) == 0 || strTable == null)
              {
                row = tbl.NewRow();
                row["TABLE_CATALOG"] = strCatalog;
                row["TABLE_NAME"] = rd.GetString(2);
                row["INDEX_NAME"] = rd.GetString(1);

                tbl.Rows.Add(row);
              }
            }
          }
        }
      }

      tbl.AcceptChanges();
      tbl.EndLoadData();

      return tbl;
    }

    /// <summary>
    /// Retrieves table schema information for the database and catalog
    /// </summary>
    /// <param name="strCatalog">The catalog (attached database) to retrieve tables on</param>
    /// <param name="strTable">The table to retrieve, can be null</param>
    /// <param name="strType">The table type, can be null</param>
    /// <returns>DataTable</returns>
    private DataTable Schema_Tables(string strCatalog, string strTable, string strType)
    {
      DataTable tbl = new DataTable("Tables");
      DataRow row;
      string strItem;

      tbl.Columns.Add("TABLE_CATALOG", typeof(string));
      tbl.Columns.Add("TABLE_SCHEMA", typeof(string));
      tbl.Columns.Add("TABLE_NAME", typeof(string));
      tbl.Columns.Add("TABLE_TYPE", typeof(string));
      tbl.Columns.Add("TABLE_GUID", typeof(Guid));
      tbl.Columns.Add("DESCRIPTION", typeof(string));
      tbl.Columns.Add("TABLE_PROPID", typeof(long));
      tbl.Columns.Add("DATE_CREATED", typeof(DateTime));
      tbl.Columns.Add("DATE_MODIFIED", typeof(DateTime));

      tbl.BeginLoadData();

      if (strCatalog == null || strCatalog == "") strCatalog = "main";

      using (SQLiteCommand cmd = new SQLiteCommand(String.Format("SELECT * FROM [{0}].[sqlite_master] WHERE [type] NOT LIKE 'index'", strCatalog), this))
      {
        using (SQLiteDataReader rd = (SQLiteDataReader)cmd.ExecuteReader())
        {
          while (rd.Read())
          {
            strItem = rd.GetString(0).ToUpper();
            if (rd.GetString(2).ToUpper().IndexOf("SQLITE_") == 0)
              strItem = "SYSTEM TABLE";

            if (String.Compare(strItem, strType, true) == 0 || strType == null)
            {
              if (String.Compare(rd.GetString(2), strTable, true) == 0 || strTable == null)
              {
                row = tbl.NewRow();
                row["TABLE_CATALOG"] = strCatalog;
                row["TABLE_NAME"] = rd.GetString(2);
                row["TABLE_TYPE"] = strItem;

                tbl.Rows.Add(row);
              }
            }
          }
        }
      }

      tbl.AcceptChanges();
      tbl.EndLoadData();

      return tbl;
    }

    /// <summary>
    /// Retrieves view schema information for the database
    /// </summary>
    /// <param name="strCatalog">The catalog (attached database) to retrieve views on</param>
    /// <param name="strView">The view name, can be null</param>
    /// <returns>DataTable</returns>
    private DataTable Schema_Views(string strCatalog, string strView)
    {
      DataTable tbl = new DataTable("Views");
      DataRow row;
      string strItem;
      int nPos;

      tbl.Columns.Add("TABLE_CATALOG", typeof(string));
      tbl.Columns.Add("TABLE_SCHEMA", typeof(string));
      tbl.Columns.Add("TABLE_NAME", typeof(string));
      tbl.Columns.Add("VIEW_DEFINITION", typeof(string));
      tbl.Columns.Add("CHECK_OPTION", typeof(bool));
      tbl.Columns.Add("IS_UPDATEABLE", typeof(bool));
      tbl.Columns.Add("DESCRIPTION", typeof(string));
      tbl.Columns.Add("DATE_CREATED", typeof(DateTime));
      tbl.Columns.Add("DATE_MODIFIED", typeof(DateTime));

      tbl.BeginLoadData();

      if (strCatalog == null || strCatalog == "") strCatalog = "main";

      using (SQLiteCommand cmd = new SQLiteCommand(String.Format("SELECT * FROM [{0}].[sqlite_master] WHERE [type] LIKE 'view'", strCatalog), this))
      {
        using (SQLiteDataReader rd = (SQLiteDataReader)cmd.ExecuteReader())
        {
          while (rd.Read())
          {
            if (String.Compare(rd.GetString(1), strView, true) == 0 || strView == null)
            {
              strItem = rd.GetString(4);
              nPos = Globalization.CultureInfo.InvariantCulture.CompareInfo.IndexOf(strItem, " AS ");
              if (nPos > -1)
              {
                strItem = strItem.Substring(nPos + 4);
                row = tbl.NewRow();
                row["TABLE_CATALOG"] = strCatalog;
                row["TABLE_NAME"] = rd.GetString(2);
                row["IS_UPDATEABLE"] = false;
                row["VIEW_DEFINITION"] = strItem;

                tbl.Rows.Add(row);
              }
            }
          }
        }
      }

      tbl.AcceptChanges();
      tbl.EndLoadData();

      return tbl;
    }

    /// <summary>
    /// Retrieves catalog (attached databases) schema information for the database
    /// </summary>
    /// <param name="strCatalog">The catalog to retrieve, can be null</param>
    /// <returns>DataTable</returns>
    private DataTable Schema_Catalogs(string strCatalog)
    {
      DataTable tbl = new DataTable("Catalogs");
      DataRow row;

      tbl.Columns.Add("CATALOG_NAME", typeof(string));
      tbl.Columns.Add("DESCRIPTION", typeof(string));

      tbl.BeginLoadData();

      using (SQLiteCommand cmd = new SQLiteCommand("PRAGMA database_list", this))
      {
        using (SQLiteDataReader rd = (SQLiteDataReader)cmd.ExecuteReader())
        {
          while (rd.Read())
          {
            if (strCatalog == null || String.Compare(rd.GetString(1), strCatalog, true) == 0)
            {
              row = tbl.NewRow();
              row["CATALOG_NAME"] = rd.GetString(1);
              tbl.Rows.Add(row);
            }
          }
        }
      }

      tbl.AcceptChanges();
      tbl.EndLoadData();

      return tbl;
    }
  }
}
