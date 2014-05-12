﻿namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Linq;
    using System.Data;
    using System.Data.SqlClient;
    using Pipeline;
    using Serializers.Json;
    using Unicast.Queuing;
    using System.Collections.Generic;

    /// <summary>
    ///     SqlServer implementation of <see cref="ISendMessages" />.
    /// </summary>
    class SqlServerMessageSender : ISendMessages
    {
        const string SqlSend =
            @"INSERT INTO [{0}] ([Id],[CorrelationId],[ReplyToAddress],[Recoverable],[Expires],[Headers],[Body]) 
                                    VALUES (@Id,@CorrelationId,@ReplyToAddress,@Recoverable,@Expires,@Headers,@Body)";

        static JsonMessageSerializer Serializer = new JsonMessageSerializer(null);

        public string DefaultConnectionString { get; set; }

        public Dictionary<string, string> ConnectionStringCollection { get; set; }

        public PipelineExecutor PipelineExecutor { get; set; }
        
        public SqlServerMessageSender()
        {
            ConnectionStringCollection = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        }

        /// <summary>
        ///     Sends the given <paramref name="message" /> to the <paramref name="address" />.
        /// </summary>
        /// <param name="message">
        ///     <see cref="TransportMessage" /> to send.
        /// </param>
        /// <param name="address">
        ///     Destination <see cref="Address" />.
        /// </param>
        public void Send(TransportMessage message, Address address)
        {
            try
            {
                //If there is a connectionstring configured for the queue, use that connectionstring
                var queueConnectionString = DefaultConnectionString;
                if (ConnectionStringCollection.Keys.Contains(address.Queue))
                {
                    queueConnectionString = ConnectionStringCollection[address.Queue];
                }

                SqlTransaction currentTransaction;


                if (PipelineExecutor.CurrentContext.TryGet(string.Format("SqlTransaction-{0}", queueConnectionString), out currentTransaction))
                {
                    using (var command = new SqlCommand(string.Format(SqlSend, TableNameUtils.GetTableName(address)), currentTransaction.Connection, currentTransaction)
                        {
                            CommandType = CommandType.Text
                        })
                    {
                        ExecuteQuery(message, command);
                    }
                }
                else
                {
                    SqlConnection currentConnection;

                    if (PipelineExecutor.CurrentContext.TryGet(string.Format("SqlConnection-{0}", queueConnectionString), out currentConnection))
                    {
                        ExecuteSendCommand(message, address, currentConnection);
                    }
                    else
                    {
                        using (var connection = new SqlConnection(queueConnectionString))
                        {
                            connection.Open();
                            ExecuteSendCommand(message, address, connection);
                        }
                        
                    }
                }
            }
            catch (SqlException ex)
            {
                if (ex.Number == 208)
                {
                    var msg = address == null
                                     ? "Failed to send message. Target address is null."
                                     : string.Format("Failed to send message to address: [{0}]", address);

                    throw new QueueNotFoundException(address, msg, ex);
                }

                ThrowFailedToSendException(address, ex);
            }
            catch (Exception ex)
            {
                ThrowFailedToSendException(address, ex);
            }
        }

        static void ExecuteSendCommand(TransportMessage message, Address address, SqlConnection connection)
        {
            using (var command = new SqlCommand(string.Format(SqlSend, TableNameUtils.GetTableName(address)), connection)
            {
                CommandType = CommandType.Text
            })
            {
                ExecuteQuery(message, command);
            }
        }


        private static void ThrowFailedToSendException(Address address, Exception ex)
        {
            if (address == null)
                throw new FailedToSendMessageException("Failed to send message.", ex);

            throw new FailedToSendMessageException(
                string.Format("Failed to send message to address: {0}@{1}", address.Queue, address.Machine), ex);
        }

        private static void ExecuteQuery(TransportMessage message, SqlCommand command)
        {
            command.Parameters.Add("Id", SqlDbType.UniqueIdentifier).Value = Guid.Parse(message.Id);
            command.Parameters.Add("CorrelationId", SqlDbType.VarChar).Value =
                GetValue(message.CorrelationId);
            if (message.ReplyToAddress == null) // SendOnly endpoint
            {
                command.Parameters.Add("ReplyToAddress", SqlDbType.VarChar).Value = DBNull.Value;
            }
            else
            {
                command.Parameters.Add("ReplyToAddress", SqlDbType.VarChar).Value =
                    message.ReplyToAddress.ToString();
            }
            command.Parameters.Add("Recoverable", SqlDbType.Bit).Value = message.Recoverable;
            if (message.TimeToBeReceived == TimeSpan.MaxValue)
            {
                command.Parameters.Add("Expires", SqlDbType.DateTime).Value = DBNull.Value;
            }
            else
            {
                command.Parameters.Add("Expires", SqlDbType.DateTime).Value =
                    DateTime.UtcNow.Add(message.TimeToBeReceived);
            }
            command.Parameters.Add("Headers", SqlDbType.VarChar).Value =
                Serializer.SerializeObject(message.Headers);
            if (message.Body == null)
            {
                command.Parameters.Add("Body", SqlDbType.VarBinary).Value = DBNull.Value;
            }
            else
            {
                command.Parameters.Add("Body", SqlDbType.VarBinary).Value = message.Body;
            }

            command.ExecuteNonQuery();
        }

        private static object GetValue(object value)
        {
            return value ?? DBNull.Value;
        }
    }
}