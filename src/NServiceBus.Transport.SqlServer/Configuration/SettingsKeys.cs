﻿namespace NServiceBus.Transport.SqlServer
{
    class SettingsKeys
    {

        public const string PurgeBatchSizeKey = "SqlServer.PurgeBatchSize";

        public const string SchemaPropertyKey = "Schema";
        public const string CatalogPropertyKey = "Catalog";

        // For backward compatibility reasons these settings keys are hard coded to the System.Data types to enable connection and transaction sharing with SQL persistence
        public const string TransportTransactionSqlConnectionKey = "System.Data.SqlClient.SqlConnection";
        public const string TransportTransactionSqlTransactionKey = "System.Data.SqlClient.SqlTransaction";

        public const string IsUserProvidedTransactionKey = "SqlServer.Transaction.IsUserProvided";
    }
}