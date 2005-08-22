/********************************************************
 * ADO.NET 2.0 Data Provider for SQLite Version 3.X
 * Written by Robert Simpson (robert@blackcastlesoft.com)
 * 
 * Released to the public domain, use at your own risk!
 ********************************************************/

namespace SQLite.Designer
{
  using System;
  using System.Collections.Generic;
  using System.Text;
  using Microsoft.VisualStudio.Data;

  internal class SQLiteDataSourceInformation : DataSourceInformation
  {
    public SQLiteDataSourceInformation()
    {
      Initialize();
    }

    public SQLiteDataSourceInformation(DataConnection connection) : base(connection)
    {
      Initialize();
    }

    private void Initialize()
    {
      AddProperty(DefaultSchema);
      AddProperty(DefaultCatalog, "main");
      AddProperty(SupportsAnsi92Sql, true);
      AddProperty(SupportsQuotedIdentifierParts, false);
      AddProperty(IdentifierOpenQuote, "[");
      AddProperty(IdentifierCloseQuote, "]");
      AddProperty(CatalogSeparator, ".");
      AddProperty(CatalogSupported, true);
      AddProperty(CatalogSupportedInDml, true);
      AddProperty(SchemaSupported, false);
      AddProperty(SchemaSupportedInDml, false);
      AddProperty(SchemaSeparator, "");
      AddProperty(ParameterPrefix, "$");
      AddProperty(ParameterPrefixInName, true);
    }
  }
}
