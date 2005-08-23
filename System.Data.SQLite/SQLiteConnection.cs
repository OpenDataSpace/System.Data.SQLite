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
  using System.Globalization;

  /// <summary>
  /// The I/O file cache flushing behavior for the connection
  /// </summary>
  public enum SynchronizationModes
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
    /// <summary>
    /// The database filename minus path and extension
    /// </summary>
    private string _dataSource;

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
            if (String.Compare(str, "main", true, CultureInfo.InvariantCulture) != 0
              && String.Compare(str, "temp", true, CultureInfo.InvariantCulture) != 0)
            {
              _sql.Execute(String.Format(CultureInfo.InvariantCulture, "ATTACH DATABASE '{0}' AS [{1}]", row[1], row[0]));
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
    /// <param name="disposing">True if the connection is being explicitly closed.</param>
    protected override void Dispose(bool disposing)
    {
      base.Dispose(disposing);
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
    /// Returns the filename without extension or path
    /// </summary>
    public override string DataSource
    {
      get 
      {
        return _dataSource;
      }
    }

    /// <summary>
    /// Returns an empty string
    /// </summary>
    public override string Database
    {
      get
      {
        return "main";
      }
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
    static internal string FindKey(KeyValuePair<string, string>[] opts, string key, string defValue)
    {
      int x = opts.Length;
      for (int n = 0; n < x; n++)
      {
        if (String.Compare(opts[n].Key, key, true, CultureInfo.InvariantCulture) == 0)
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

      if (Convert.ToInt32(FindKey(opts, "Version", "3"), CultureInfo.InvariantCulture) != 3)
        throw new NotSupportedException("Only SQLite Version 3 is supported at this time");

      string strFile = FindKey(opts, "Data Source", "");

      if (String.IsNullOrEmpty(strFile))
        throw new ArgumentException("Data Source cannot be empty.  Use :MEMORY: to open an in-memory database");

      try
      {
        bool bUTF16 = (Convert.ToBoolean(FindKey(opts, "UseUTF16Encoding", "False"), CultureInfo.InvariantCulture) == true);
        SQLiteDateFormats dateFormat = String.Compare(FindKey(opts, "DateTimeFormat", "ISO8601"), "ticks", true, CultureInfo.CurrentCulture) == 0 ? SQLiteDateFormats.Ticks : SQLiteDateFormats.ISO8601;

        if (bUTF16)
          _sql = new SQLite3_UTF16(dateFormat);
        else
          _sql = new SQLite3(dateFormat);

          _sql.Open(strFile);

        _dataSource = System.IO.Path.GetFileNameWithoutExtension(strFile);

        if (bUTF16 == true)
          _sql.Execute("PRAGMA encoding = 'UTF-16'");
        else
          _sql.Execute("PRAGMA encoding = 'UTF-8'");

        _sql.Execute(String.Format(CultureInfo.InvariantCulture, "PRAGMA Synchronous={0}", FindKey(opts, "Synchronous", "Normal")));
        _sql.Execute(String.Format(CultureInfo.InvariantCulture, "PRAGMA Cache_Size={0}", FindKey(opts, "Cache Size", "2000")));
        if (String.Compare(":MEMORY:", strFile, true, CultureInfo.CurrentCulture) == 0)
          _sql.Execute(String.Format(CultureInfo.InvariantCulture, "PRAGMA Page_Size={0}", FindKey(opts, "Page Size", "1024")));
      }
      catch (SQLiteException)
      {
        OnStateChange(ConnectionState.Broken);
        throw;
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
      get
      {
        return _connectionState;
      }
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
      switch (collectionName.ToUpper(CultureInfo.CurrentCulture))
      {
        case "METADATACOLLECTIONS":
          return Schema_MetaDataCollections();
        case "DATASOURCEINFORMATION":
          return Schema_DataSourceInformation();
        //case "RESERVEDWORDS":
        //  return Schema_ReservedWords();
        case "DATATYPES":
          return Schema_DataTypes();
        case "COLUMNS":
          return Schema_Columns(parms[0], parms[2], parms[3]);
        case "INDEXES":
          return Schema_Indexes(parms[0], parms[2], parms[4]);
        case "INDEXCOLUMNS":
          return Schema_IndexColumns(parms[0], parms[2], parms[3], parms[4]);
        case "TABLES":
          return Schema_Tables(parms[0], parms[2], parms[3]);
        case "VIEWS":
          return Schema_Views(parms[0], parms[2]);
        case "VIEWCOLUMNS":
          return Schema_ViewColumns(parms[0], parms[2], parms[3]);
        case "FOREIGNKEYS":
          return Schema_ForeignKeys(parms[0], parms[2], parms[3]);
        case "CATALOGS":
          return Schema_Catalogs(parms[0]);
      }
      throw new NotSupportedException();
    }

    /// <summary>
    /// Builds a MetaDataCollections schema datatable
    /// </summary>
    /// <returns>DataTable</returns>
    private static DataTable Schema_MetaDataCollections()
    {
      DataTable tbl = new DataTable("MetaDataCollections");
      DataRow row;

      tbl.Locale = CultureInfo.InvariantCulture;
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
      row.ItemArray = new object[] { "DataTypes", 0, 0 };
      tbl.Rows.Add(row);

      //row = tbl.NewRow();
      //row.ItemArray = new object[] { "ReservedWords", 0, 0 };
      //tbl.Rows.Add(row);

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
      row.ItemArray = new object[] { "IndexColumns", 5, 4 };
      tbl.Rows.Add(row);

      row = tbl.NewRow();
      row.ItemArray = new object[] { "Tables", 4, 3 };
      tbl.Rows.Add(row);

      row = tbl.NewRow();
      row.ItemArray = new object[] { "Views", 3, 3 };
      tbl.Rows.Add(row);

      row = tbl.NewRow();
      row.ItemArray = new object[] { "ViewColumns", 4, 4 };
      tbl.Rows.Add(row);

      row = tbl.NewRow();
      row.ItemArray = new object[] { "ForeignKeys", 4, 3 };
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

      tbl.Locale = CultureInfo.InvariantCulture;
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
        null,
        "SQLite",
        _sql.Version,
        _sql.Version,
        3,
        null,
        2,
        false,
        "?",
        "?",
        0,
        null,
        @"(([^\[]|\]\])*)",
        2,
        ";",
        @"'(([^']|'')*)'",
        null
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

      tbl.Locale = CultureInfo.InvariantCulture;
      tbl.Columns.Add("TABLE_CATALOG", typeof(string));
      tbl.Columns.Add("TABLE_SCHEMA", typeof(string));
      tbl.Columns.Add("TABLE_NAME", typeof(string));
      tbl.Columns.Add("COLUMN_NAME", typeof(string));
      tbl.Columns.Add("COLUMN_GUID", typeof(Guid));
      tbl.Columns.Add("COLUMN_PROPID", typeof(long));
      tbl.Columns.Add("ORDINAL_POSITION", typeof(int));
      tbl.Columns.Add("COLUMN_HASDEFAULT", typeof(bool));
      tbl.Columns.Add("COLUMN_DEFAULT", typeof(string));
      tbl.Columns.Add("COLUMN_FLAGS", typeof(long));
      tbl.Columns.Add("IS_NULLABLE", typeof(bool));
      tbl.Columns.Add("DATA_TYPE", typeof(string));
      tbl.Columns.Add("TYPE_GUID", typeof(Guid));
      tbl.Columns.Add("CHARACTER_MAXIMUM_LENGTH", typeof(int));
      tbl.Columns.Add("CHARACTER_OCTET_LENGTH", typeof(int));
      tbl.Columns.Add("NUMERIC_PRECISION", typeof(int));
      tbl.Columns.Add("NUMERIC_SCALE", typeof(int));
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

      if (String.IsNullOrEmpty(strCatalog)) strCatalog = "main";

      using (SQLiteCommand cmdTables = new SQLiteCommand(String.Format(CultureInfo.CurrentCulture, "SELECT * FROM [{0}].[sqlite_master] WHERE [type] LIKE 'table'", strCatalog), this))
      {
        using (SQLiteDataReader rdTables = cmdTables.ExecuteReader())
        {
          while (rdTables.Read())
          {
            if (String.IsNullOrEmpty(strTable) || String.Compare(strTable, rdTables.GetString(2), true, CultureInfo.CurrentCulture) == 0)
            {
              using (SQLiteCommand cmd = new SQLiteCommand(String.Format(CultureInfo.CurrentCulture, "SELECT * FROM [{0}].[{1}]", strCatalog, rdTables.GetString(2)), this))
              {
                using (SQLiteDataReader rd = (SQLiteDataReader)cmd.ExecuteReader(CommandBehavior.SchemaOnly))
                {
                  using (DataTable tblSchema = rd.GetSchemaTable())
                  {
                    foreach (DataRow schemaRow in tblSchema.Rows)
                    {
                      if (String.Compare(schemaRow[SchemaTableColumn.ColumnName].ToString(), strColumn, true, CultureInfo.CurrentCulture) == 0
                        || strColumn == null)
                      {
                        row = tbl.NewRow();

                        row["TABLE_NAME"] = rdTables.GetString(2);
                        row["COLUMN_NAME"] = schemaRow[SchemaTableColumn.ColumnName];
                        row["TABLE_CATALOG"] = strCatalog;
                        row["ORDINAL_POSITION"] = schemaRow[SchemaTableColumn.ColumnOrdinal];
                        row["COLUMN_HASDEFAULT"] = (schemaRow[SchemaTableOptionalColumn.DefaultValue] != DBNull.Value);
                        row["COLUMN_DEFAULT"] = schemaRow[SchemaTableOptionalColumn.DefaultValue];
                        row["IS_NULLABLE"] = schemaRow[SchemaTableColumn.AllowDBNull];
                        row["DATA_TYPE"] = SQLiteConvert.DbTypeToType((DbType)schemaRow[SchemaTableColumn.ProviderType]).ToString();
                        row["CHARACTER_MAXIMUM_LENGTH"] = schemaRow[SchemaTableColumn.ColumnSize];
                        row["TABLE_SCHEMA"] = schemaRow[SchemaTableColumn.BaseSchemaName];

                        tbl.Rows.Add(row);
                      }
                    }
                  }
                }
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

      tbl.Locale = CultureInfo.InvariantCulture;
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
      tbl.Columns.Add("ORDINAL_POSITION", typeof(int));
      tbl.Columns.Add("COLUMN_NAME", typeof(string));
      tbl.Columns.Add("COLUMN_GUID", typeof(Guid));
      tbl.Columns.Add("COLUMN_PROPID", typeof(long));
      tbl.Columns.Add("COLLATION", typeof(short));
      tbl.Columns.Add("CARDINALITY", typeof(Decimal));
      tbl.Columns.Add("PAGES", typeof(int));
      tbl.Columns.Add("FILTER_CONDITION", typeof(string));
      tbl.Columns.Add("INTEGRATED", typeof(bool));

      tbl.BeginLoadData();

      if (String.IsNullOrEmpty(strCatalog)) strCatalog = "main";

      using (SQLiteCommand cmd = new SQLiteCommand(String.Format(CultureInfo.CurrentCulture, "SELECT * FROM [{0}].[sqlite_master] WHERE [type] = 'index'", strCatalog), this))
      {
        using (SQLiteDataReader rd = (SQLiteDataReader)cmd.ExecuteReader())
        {
          while (rd.Read())
          {
            if (String.Compare(rd.GetString(1), strIndex, true, CultureInfo.CurrentCulture) == 0
            || strIndex == null)
            {
              if (String.Compare(rd.GetString(2), strTable, true, CultureInfo.CurrentCulture) == 0 
              || strTable == null)
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

      tbl.Locale = CultureInfo.InvariantCulture;
      tbl.Columns.Add("TABLE_CATALOG", typeof(string));
      tbl.Columns.Add("TABLE_SCHEMA", typeof(string));
      tbl.Columns.Add("TABLE_NAME", typeof(string));
      tbl.Columns.Add("TABLE_TYPE", typeof(string));

      tbl.BeginLoadData();

      if (String.IsNullOrEmpty(strCatalog)) strCatalog = "main";

      using (SQLiteCommand cmd = new SQLiteCommand(String.Format(CultureInfo.CurrentCulture, "SELECT * FROM [{0}].[sqlite_master] WHERE [type] NOT LIKE 'index'", strCatalog), this))
      {
        using (SQLiteDataReader rd = (SQLiteDataReader)cmd.ExecuteReader())
        {
          while (rd.Read())
          {
            strItem = rd.GetString(0);
            if (String.Compare(rd.GetString(2), 0, "SQLITE_", 0, 7, true, CultureInfo.CurrentCulture) == 0)
              strItem = "SYSTEM_TABLE";

            if (String.Compare(strType, strItem, true, CultureInfo.CurrentCulture) == 0
              || strType == null)
            {
              if (String.Compare(rd.GetString(2), strTable, true, CultureInfo.CurrentCulture) == 0
                || strTable == null)
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

      tbl.Locale = CultureInfo.InvariantCulture;
      tbl.Columns.Add("TABLE_CATALOG", typeof(string));
      tbl.Columns.Add("TABLE_SCHEMA", typeof(string));
      tbl.Columns.Add("TABLE_NAME", typeof(string));
      tbl.Columns.Add("VIEW_DEFINITION", typeof(string));
      tbl.Columns.Add("CHECK_OPTION", typeof(bool));
      tbl.Columns.Add("IS_UPDATABLE", typeof(bool));
      tbl.Columns.Add("DESCRIPTION", typeof(string));
      tbl.Columns.Add("DATE_CREATED", typeof(DateTime));
      tbl.Columns.Add("DATE_MODIFIED", typeof(DateTime));

      tbl.BeginLoadData();

      if (String.IsNullOrEmpty(strCatalog)) strCatalog = "main";

      using (SQLiteCommand cmd = new SQLiteCommand(String.Format(CultureInfo.CurrentCulture, "SELECT * FROM [{0}].[sqlite_master] WHERE [type] LIKE 'view'", strCatalog), this))
      {
        using (SQLiteDataReader rd = (SQLiteDataReader)cmd.ExecuteReader())
        {
          while (rd.Read())
          {
            if (String.Compare(rd.GetString(1), strView, true, CultureInfo.CurrentCulture) == 0
              || strView == null)
            {
              strItem = rd.GetString(4);
              nPos = Globalization.CultureInfo.InvariantCulture.CompareInfo.IndexOf(strItem, " AS ");
              if (nPos > -1)
              {
                strItem = strItem.Substring(nPos + 4);
                row = tbl.NewRow();

                row["TABLE_CATALOG"] = strCatalog;
                row["TABLE_NAME"] = rd.GetString(2);
                row["IS_UPDATABLE"] = false;
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

      tbl.Locale = CultureInfo.InvariantCulture;
      tbl.Columns.Add("CATALOG_NAME", typeof(string));
      tbl.Columns.Add("DESCRIPTION", typeof(string));

      tbl.BeginLoadData();

      using (SQLiteCommand cmd = new SQLiteCommand("PRAGMA database_list", this))
      {
        using (SQLiteDataReader rd = (SQLiteDataReader)cmd.ExecuteReader())
        {
          while (rd.Read())
          {
            if (String.Compare(rd.GetString(1), strCatalog, true, CultureInfo.CurrentCulture) == 0
              || strCatalog == null)
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

    //private DataTable Schema_ReservedWords()
    //{
    //  DataTable tbl = new DataTable("ReservedWords");
    //  DataRow row;
    //  const string reservedWords = "LEFT INNER OUTER JOIN SELECT INSERT UPDATE LIKE ORDER BY INTEGER PRIMARY KEY ON AS IN BETWEEN";

    //  tbl.Locale = CultureInfo.InvariantCulture;
    //  tbl.Columns.Add("ReservedWord", typeof(String));

    //  tbl.BeginLoadData();

    //  string[] ar = reservedWords.Split(' ');

    //  foreach (string s in ar)
    //  {
    //    row = tbl.NewRow();
    //    row[0] = s;
    //    tbl.Rows.Add(row);
    //  }

    //  tbl.AcceptChanges();
    //  tbl.EndLoadData();

    //  return tbl;
    //}

    private DataTable Schema_DataTypes()
    {
      DataTable tbl = new DataTable("DataTypes");

      tbl.Locale = CultureInfo.InvariantCulture;
      tbl.Columns.Add("TypeName", typeof(String));
      tbl.Columns.Add("ProviderDbType", typeof(int));
      tbl.Columns.Add("ColumnSize", typeof(long));
      tbl.Columns.Add("CreateFormat", typeof(String));
      tbl.Columns.Add("CreateParameters", typeof(String));
      tbl.Columns.Add("DataType", typeof(String));
      tbl.Columns.Add("IsAutoIncrementable", typeof(bool));
      tbl.Columns.Add("IsBestMatch", typeof(bool));
      tbl.Columns.Add("IsCaseSensitive", typeof(bool));
      tbl.Columns.Add("IsFixedLength", typeof(bool));
      tbl.Columns.Add("IsFixedPrecisionScale", typeof(bool));
      tbl.Columns.Add("IsLong", typeof(bool));
      tbl.Columns.Add("IsNullable", typeof(bool));
      tbl.Columns.Add("IsSearchable", typeof(bool));
      tbl.Columns.Add("IsSearchableWithLike", typeof(bool));
      tbl.Columns.Add("IsLiteralSupported", typeof(bool));
      tbl.Columns.Add("LiteralPrefix", typeof(String));
      tbl.Columns.Add("LiteralSuffix", typeof(String));
      tbl.Columns.Add("IsUnsigned", typeof(bool));
      tbl.Columns.Add("MaximumScale", typeof(short));
      tbl.Columns.Add("MinimumScale", typeof(short));
      tbl.Columns.Add("IsConcurrencyType", typeof(bool));

      tbl.BeginLoadData();
      string dataTypesXml = @"<?xml version=""1.0"" standalone=""yes""?>
<DocumentElement>
  <DataTypes>
    <TypeName>System.Int16</TypeName>
    <ProviderDbType>10</ProviderDbType>
    <ColumnSize>5</ColumnSize>
    <DataType>System.Int16</DataType>
    <IsAutoIncrementable>false</IsAutoIncrementable>
    <IsCaseSensitive>false</IsCaseSensitive>
    <IsFixedLength>true</IsFixedLength>
    <IsFixedPrecisionScale>true</IsFixedPrecisionScale>
    <IsLong>false</IsLong>
    <IsNullable>true</IsNullable>
    <IsSearchable>true</IsSearchable>
    <IsSearchableWithLike>false</IsSearchableWithLike>
    <IsUnsigned>false</IsUnsigned>
  </DataTypes>
  <DataTypes>
    <TypeName>System.Int32</TypeName>
    <ProviderDbType>8</ProviderDbType>
    <ColumnSize>10</ColumnSize>
    <DataType>System.Int32</DataType>
    <IsAutoIncrementable>false</IsAutoIncrementable>
    <IsCaseSensitive>false</IsCaseSensitive>
    <IsFixedLength>true</IsFixedLength>
    <IsFixedPrecisionScale>true</IsFixedPrecisionScale>
    <IsLong>false</IsLong>
    <IsNullable>true</IsNullable>
    <IsSearchable>true</IsSearchable>
    <IsSearchableWithLike>false</IsSearchableWithLike>
    <IsUnsigned>false</IsUnsigned>
  </DataTypes>
  <DataTypes>
    <TypeName>System.Single</TypeName>
    <ProviderDbType>15</ProviderDbType>
    <ColumnSize>7</ColumnSize>
    <DataType>System.Single</DataType>
    <IsAutoIncrementable>false</IsAutoIncrementable>
    <IsCaseSensitive>false</IsCaseSensitive>
    <IsFixedLength>true</IsFixedLength>
    <IsFixedPrecisionScale>false</IsFixedPrecisionScale>
    <IsLong>false</IsLong>
    <IsNullable>true</IsNullable>
    <IsSearchable>true</IsSearchable>
    <IsSearchableWithLike>false</IsSearchableWithLike>
    <IsUnsigned>false</IsUnsigned>
  </DataTypes>
  <DataTypes>
    <TypeName>System.Double</TypeName>
    <ProviderDbType>8</ProviderDbType>
    <ColumnSize>6</ColumnSize>
    <DataType>System.Double</DataType>
    <IsAutoIncrementable>false</IsAutoIncrementable>
    <IsCaseSensitive>false</IsCaseSensitive>
    <IsFixedLength>true</IsFixedLength>
    <IsFixedPrecisionScale>false</IsFixedPrecisionScale>
    <IsLong>false</IsLong>
    <IsNullable>true</IsNullable>
    <IsSearchable>true</IsSearchable>
    <IsSearchableWithLike>false</IsSearchableWithLike>
    <IsUnsigned>false</IsUnsigned>
  </DataTypes>
  <DataTypes>
    <TypeName>System.Decimal</TypeName>
    <ProviderDbType>7</ProviderDbType>
    <ColumnSize>19</ColumnSize>
    <DataType>System.Decimal</DataType>
    <IsAutoIncrementable>false</IsAutoIncrementable>
    <IsCaseSensitive>false</IsCaseSensitive>
    <IsFixedLength>true</IsFixedLength>
    <IsFixedPrecisionScale>true</IsFixedPrecisionScale>
    <IsLong>false</IsLong>
    <IsNullable>true</IsNullable>
    <IsSearchable>true</IsSearchable>
    <IsSearchableWithLike>false</IsSearchableWithLike>
    <IsUnsigned>false</IsUnsigned>
  </DataTypes>
  <DataTypes>
    <TypeName>System.Boolean</TypeName>
    <ProviderDbType>3</ProviderDbType>
    <ColumnSize>1</ColumnSize>
    <DataType>System.Boolean</DataType>
    <IsAutoIncrementable>false</IsAutoIncrementable>
    <IsCaseSensitive>false</IsCaseSensitive>
    <IsFixedLength>true</IsFixedLength>
    <IsFixedPrecisionScale>false</IsFixedPrecisionScale>
    <IsLong>false</IsLong>
    <IsNullable>true</IsNullable>
    <IsSearchable>true</IsSearchable>
    <IsSearchableWithLike>false</IsSearchableWithLike>
  </DataTypes>
  <DataTypes>
    <TypeName>System.Byte</TypeName>
    <ProviderDbType>2</ProviderDbType>
    <ColumnSize>3</ColumnSize>
    <DataType>System.Byte</DataType>
    <IsAutoIncrementable>false</IsAutoIncrementable>
    <IsCaseSensitive>false</IsCaseSensitive>
    <IsFixedLength>true</IsFixedLength>
    <IsFixedPrecisionScale>true</IsFixedPrecisionScale>
    <IsLong>false</IsLong>
    <IsNullable>true</IsNullable>
    <IsSearchable>true</IsSearchable>
    <IsSearchableWithLike>false</IsSearchableWithLike>
    <IsUnsigned>true</IsUnsigned>
  </DataTypes>
  <DataTypes>
    <TypeName>System.Int64</TypeName>
    <ProviderDbType>12</ProviderDbType>
    <ColumnSize>19</ColumnSize>
    <DataType>System.Int64</DataType>
    <IsAutoIncrementable>true</IsAutoIncrementable>
    <IsCaseSensitive>false</IsCaseSensitive>
    <IsFixedLength>true</IsFixedLength>
    <IsFixedPrecisionScale>true</IsFixedPrecisionScale>
    <IsLong>false</IsLong>
    <IsNullable>true</IsNullable>
    <IsSearchable>true</IsSearchable>
    <IsSearchableWithLike>false</IsSearchableWithLike>
    <IsUnsigned>false</IsUnsigned>
  </DataTypes>
  <DataTypes>
    <TypeName>System.Byte[]</TypeName>
    <ProviderDbType>1</ProviderDbType>
    <ColumnSize>2147483647</ColumnSize>
    <DataType>System.Byte[]</DataType>
    <IsAutoIncrementable>false</IsAutoIncrementable>
    <IsCaseSensitive>false</IsCaseSensitive>
    <IsFixedLength>false</IsFixedLength>
    <IsFixedPrecisionScale>false</IsFixedPrecisionScale>
    <IsLong>true</IsLong>
    <IsNullable>true</IsNullable>
    <IsSearchable>false</IsSearchable>
    <IsSearchableWithLike>false</IsSearchableWithLike>
    <LiteralPrefix>X'</LiteralPrefix>
    <LiteralSuffix>'</LiteralSuffix>
  </DataTypes>
  <DataTypes>
    <TypeName>System.String</TypeName>
    <ProviderDbType>16</ProviderDbType>
    <ColumnSize>2147483647</ColumnSize>
    <CreateParameters>max length</CreateParameters>
    <DataType>System.String</DataType>
    <IsAutoIncrementable>false</IsAutoIncrementable>
    <IsCaseSensitive>false</IsCaseSensitive>
    <IsFixedLength>false</IsFixedLength>
    <IsFixedPrecisionScale>false</IsFixedPrecisionScale>
    <IsLong>false</IsLong>
    <IsNullable>true</IsNullable>
    <IsSearchable>true</IsSearchable>
    <IsSearchableWithLike>true</IsSearchableWithLike>
    <LiteralPrefix>'</LiteralPrefix>
    <LiteralSuffix>'</LiteralSuffix>
  </DataTypes>
  <DataTypes>
    <TypeName>System.DateTime</TypeName>
    <ProviderDbType>6</ProviderDbType>
    <ColumnSize>23</ColumnSize>
    <DataType>System.DateTime</DataType>
    <IsAutoIncrementable>false</IsAutoIncrementable>
    <IsCaseSensitive>false</IsCaseSensitive>
    <IsFixedLength>true</IsFixedLength>
    <IsFixedPrecisionScale>false</IsFixedPrecisionScale>
    <IsLong>false</IsLong>
    <IsNullable>true</IsNullable>
    <IsSearchable>true</IsSearchable>
    <IsSearchableWithLike>true</IsSearchableWithLike>
    <LiteralPrefix>'</LiteralPrefix>
    <LiteralSuffix>'</LiteralSuffix>
  </DataTypes>
  <DataTypes>
    <TypeName>System.Guid</TypeName>
    <ProviderDbType>4</ProviderDbType>
    <ColumnSize>16</ColumnSize>
    <DataType>System.Guid</DataType>
    <IsAutoIncrementable>false</IsAutoIncrementable>
    <IsCaseSensitive>false</IsCaseSensitive>
    <IsFixedLength>true</IsFixedLength>
    <IsFixedPrecisionScale>false</IsFixedPrecisionScale>
    <IsLong>false</IsLong>
    <IsNullable>true</IsNullable>
    <IsSearchable>true</IsSearchable>
    <IsSearchableWithLike>false</IsSearchableWithLike>
    <LiteralPrefix>'</LiteralPrefix>
    <LiteralSuffix>'</LiteralSuffix>
  </DataTypes>
</DocumentElement>";

      IO.StringReader stringReader = new System.IO.StringReader(dataTypesXml);
      tbl.ReadXml(stringReader);
      stringReader.Close();

      tbl.AcceptChanges();
      tbl.EndLoadData();

      return tbl;
    }

    /// <summary>
    /// Returns the base column information for indexes in a database
    /// </summary>
    /// <param name="strCatalog">The catalog to retrieve indexes for (can be null)</param>
    /// <param name="strTable">The table to restrict index information by (can be null)</param>
    /// <param name="strIndex">The index to restrict index information by (can be null)</param>
    /// <param name="strColumn">The source column to restrict index information by (can be null)</param>
    /// <returns>A DataTable containing the results</returns>
    private DataTable Schema_IndexColumns(string strCatalog, string strTable, string strIndex, string strColumn)
    {
      DataTable tbl = new DataTable("IndexColumns");
      DataRow row;

      tbl.Locale = CultureInfo.InvariantCulture;
      tbl.Columns.Add("CONSTRAINT_CATALOG", typeof(string));
      tbl.Columns.Add("CONSTRAINT_SCHEMA", typeof(string));
      tbl.Columns.Add("CONSTRAINT_NAME", typeof(string));
      tbl.Columns.Add("TABLE_CATALOG", typeof(string));
      tbl.Columns.Add("TABLE_SCHEMA", typeof(string));
      tbl.Columns.Add("TABLE_NAME", typeof(string));
      tbl.Columns.Add("COLUMN_NAME", typeof(string));
      tbl.Columns.Add("ORDINAL_POSITION", typeof(int));
      tbl.Columns.Add("INDEX_NAME", typeof(string));

      if (String.IsNullOrEmpty(strCatalog)) strCatalog = "main";

      tbl.BeginLoadData();

      using (SQLiteCommand cmdTable = new SQLiteCommand(String.Format(CultureInfo.CurrentCulture, "SELECT * FROM [{0}].[sqlite_master] WHERE [type] LIKE 'index'", strCatalog), this))
      {
        using (SQLiteDataReader rdTable = cmdTable.ExecuteReader())
        {
          while (rdTable.Read())
          {
            if (String.IsNullOrEmpty(strTable) || String.Compare(strTable, rdTable.GetString(2), true, CultureInfo.CurrentCulture) == 0)
            {
              if (String.IsNullOrEmpty(strIndex) || String.Compare(strIndex, rdTable.GetString(1), true, CultureInfo.CurrentCulture) == 0)
              {
                using (SQLiteCommand cmdIndex = new SQLiteCommand(String.Format(CultureInfo.CurrentCulture, "PRAGMA [{0}].index_info([{1}])", strCatalog, rdTable.GetString(1)), this))
                {
                  using (SQLiteDataReader rdIndex = cmdIndex.ExecuteReader())
                  {
                    while (rdIndex.Read())
                    {
                      row = tbl.NewRow();
                      row["CONSTRAINT_CATALOG"] = strCatalog;
                      row["CONSTRAINT_NAME"] = rdTable.GetString(1);
                      row["TABLE_CATALOG"] = strCatalog;
                      row["TABLE_NAME"] = rdTable.GetString(2);
                      row["COLUMN_NAME"] = rdIndex.GetString(2);
                      row["INDEX_NAME"] = rdTable.GetString(1);
                      row["ORDINAL_POSITION"] = rdIndex.GetInt32(1);

                      tbl.Rows.Add(row);
                    }
                  }
                }
              }
            }
          }
        }
      }

      tbl.EndLoadData();
      tbl.AcceptChanges();

      return tbl;
    }

    /// <summary>
    /// Returns detailed column information for a specified view
    /// </summary>
    /// <param name="strCatalog">The catalog to retrieve columns for (can be null)</param>
    /// <param name="strView">The view to restrict column information by (can be null)</param>
    /// <param name="strColumn">The source column to restrict column information by (can be null)</param>
    /// <returns>A DataTable containing the results</returns>
    private DataTable Schema_ViewColumns(string strCatalog, string strView, string strColumn)
    {
      DataTable tbl = new DataTable("ViewColumns");
      DataRow row;
      string strSql;
      int n;
      DataRow schemaRow;
      DataRow viewRow;

      tbl.Locale = CultureInfo.InvariantCulture;
      tbl.Columns.Add("VIEW_CATALOG", typeof(string));
      tbl.Columns.Add("VIEW_SCHEMA", typeof(string));
      tbl.Columns.Add("VIEW_NAME", typeof(string));
      tbl.Columns.Add("VIEW_COLUMN_NAME", typeof(String));
      tbl.Columns.Add("TABLE_CATALOG", typeof(string));
      tbl.Columns.Add("TABLE_SCHEMA", typeof(string));
      tbl.Columns.Add("TABLE_NAME", typeof(string));
      tbl.Columns.Add("COLUMN_NAME", typeof(string));

      if (String.IsNullOrEmpty(strCatalog)) strCatalog = "main";

      tbl.BeginLoadData();

      using (SQLiteCommand cmdViews = new SQLiteCommand(String.Format(CultureInfo.CurrentCulture, "SELECT * FROM [{0}].[sqlite_master] WHERE [type] LIKE 'view'", strCatalog), this))
      {
        using (SQLiteDataReader rdViews = cmdViews.ExecuteReader())
        {
          while (rdViews.Read())
          {
            if (String.IsNullOrEmpty(strView) || String.Compare(strView, rdViews.GetString(2), true, CultureInfo.CurrentCulture) == 0)
            {
              using (SQLiteCommand cmdViewSelect = new SQLiteCommand(String.Format(CultureInfo.CurrentCulture, "SELECT * FROM [{0}].[{1}]", strCatalog, rdViews.GetString(2)), this))
              {
                strSql = rdViews.GetString(4);
                n = CultureInfo.CurrentCulture.CompareInfo.IndexOf(strSql, " AS ", CompareOptions.IgnoreCase);
                if (n < 0)
                  continue;

                strSql = strSql.Substring(n + 4);

                using (SQLiteCommand cmd = new SQLiteCommand(strSql, this))
                {
                  using (SQLiteDataReader rdViewSelect = cmdViewSelect.ExecuteReader(CommandBehavior.SchemaOnly))
                  {
                    using (SQLiteDataReader rd = (SQLiteDataReader)cmd.ExecuteReader(CommandBehavior.SchemaOnly))
                    {
                      using (DataTable tblSchemaView = rdViewSelect.GetSchemaTable())
                      {
                        using (DataTable tblSchema = rd.GetSchemaTable())
                        {
                          for (n = 0; n < tblSchema.Rows.Count; n++)
                          {
                            viewRow = tblSchemaView.Rows[n];
                            schemaRow = tblSchema.Rows[n];

                            if (String.Compare(viewRow[SchemaTableColumn.ColumnName].ToString(), strColumn, true, CultureInfo.CurrentCulture) == 0
                              || strColumn == null)
                            {
                              row = tbl.NewRow();

                              row["VIEW_CATALOG"] = strCatalog;
                              row["VIEW_NAME"] = rdViews.GetString(2);
                              row["TABLE_CATALOG"] = strCatalog;
                              row["TABLE_SCHEMA"] = schemaRow[SchemaTableColumn.BaseSchemaName];
                              row["TABLE_NAME"] = schemaRow[SchemaTableColumn.BaseTableName];
                              row["COLUMN_NAME"] = schemaRow[SchemaTableColumn.ColumnName];
                              row["VIEW_COLUMN_NAME"] = viewRow[SchemaTableColumn.ColumnName];

                              tbl.Rows.Add(row);
                            }
                          }
                        }
                      }
                    }
                  }
                }
              }
            }
          }
        }
      }

      tbl.EndLoadData();
      tbl.AcceptChanges();

      return tbl;
    }

    /// <summary>
    /// Retrieves foreign key information from the specified set of filters
    /// </summary>
    /// <param name="strCatalog">An optional catalog to restrict results on</param>
    /// <param name="strTable">An optional table to restrict results on</param>
    /// <param name="strKeyName">An optional foreign key name to restrict results on</param>
    /// <returns>A DataTable with the results of the query</returns>
    private DataTable Schema_ForeignKeys(string strCatalog, string strTable, string strKeyName)
    {
      DataTable tbl = new DataTable("ForeignKeys");
      DataRow row;

      tbl.Locale = CultureInfo.InvariantCulture;
      tbl.Columns.Add("CONSTRAINT_CATALOG", typeof(string));
      tbl.Columns.Add("CONSTRAINT_SCHEMA", typeof(string));
      tbl.Columns.Add("CONSTRAINT_NAME", typeof(string));
      tbl.Columns.Add("TABLE_CATALOG", typeof(string));
      tbl.Columns.Add("TABLE_SCHEMA", typeof(string));
      tbl.Columns.Add("TABLE_NAME", typeof(string));
      tbl.Columns.Add("CONSTRAINT_TYPE", typeof(string));
      tbl.Columns.Add("IS_DEFERRABLE", typeof(bool));
      tbl.Columns.Add("INITIALLY_DEFERRED", typeof(bool));
      tbl.Columns.Add("FKEY_FROM_COLUMN", typeof(string));
      tbl.Columns.Add("FKEY_TO_CATALOG", typeof(string));
      tbl.Columns.Add("FKEY_TO_SCHEMA", typeof(string));
      tbl.Columns.Add("FKEY_TO_TABLE", typeof(string));
      tbl.Columns.Add("FKEY_TO_COLUMN", typeof(string));

      if (String.IsNullOrEmpty(strCatalog)) strCatalog = "main";

      tbl.BeginLoadData();

      using (SQLiteCommand cmdTables = new SQLiteCommand(String.Format(CultureInfo.CurrentCulture, "SELECT * FROM [{0}].[sqlite_master] WHERE [type] LIKE 'table'", strCatalog), this))
      {
        using (SQLiteDataReader rdTables = cmdTables.ExecuteReader())
        {
          while (rdTables.Read())
          {
            if (String.IsNullOrEmpty(strTable) || String.Compare(strTable, rdTables.GetString(2), true, CultureInfo.CurrentCulture) == 0)
            {
              using (SQLiteCommand cmdKey = new SQLiteCommand(String.Format(CultureInfo.CurrentCulture, "PRAGMA [{0}].foreign_key_list([{1}])", strCatalog, rdTables.GetString(2)), this))
              {
                using (SQLiteDataReader rdKey = cmdKey.ExecuteReader())
                {
                  while (rdKey.Read())
                  {
                    row = tbl.NewRow();
                    row["CONSTRAINT_CATALOG"] = strCatalog;
                    row["CONSTRAINT_NAME"] = String.Format(CultureInfo.CurrentCulture, "unnamed{0}", rdKey.GetInt64(0));
                    row["TABLE_CATALOG"] = strCatalog;
                    row["TABLE_NAME"] = rdTables.GetString(2);
                    row["CONSTRAINT_TYPE"] = "FOREIGN KEY";
                    row["IS_DEFERRABLE"] = false;
                    row["INITIALLY_DEFERRED"] = false;
                    row["FKEY_TO_CATALOG"] = strCatalog;
                    row["FKEY_TO_TABLE"] = rdKey.GetString(2);
                    row["FKEY_FROM_COLUMN"] = rdKey.GetString(3);
                    row["FKEY_TO_COLUMN"] = rdKey.GetString(4);

                    tbl.Rows.Add(row);
                  }
                }
              }
            }
          }
        }
      }

      tbl.EndLoadData();
      tbl.AcceptChanges();

      return tbl;
    }
  }
}
