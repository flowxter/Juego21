using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Blackjack.Core.Rules;
using Blackjack.Data.History;
using Blackjack.Data.Wallet;
using Blackjack.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Blackjack.Server.Tables
{
    /// <summary>
    /// Registro de mesas vivas. Cada mesa es un <see cref="TableActor"/> con
    /// su propio bucle, así que dos mesas nunca se estorban entre sí.
    /// </summary>
    public sealed class TableManager : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, TableActor> _tables = new();
        private readonly IHubContext<GameHub> _hub;
        private readonly IWalletService _wallet;
        private readonly IRoundArchive _archive;
        private readonly ILoggerFactory _loggerFactory;
        private readonly TableOptions _options;

        public TableManager(
            IHubContext<GameHub> hub,
            IWalletService wallet,
            IRoundArchive archive,
            ILoggerFactory loggerFactory,
            IOptions<TableOptions> options)
        {
            _hub = hub;
            _wallet = wallet;
            _archive = archive;
            _loggerFactory = loggerFactory;
            _options = options.Value;
        }

        /// <summary>
        /// Devuelve la mesa, creándola si es la primera vez que alguien entra.
        /// </summary>
        public TableActor GetOrCreate(string tableId)
        {
            if (string.IsNullOrWhiteSpace(tableId))
                throw new ArgumentException("Hace falta un identificador de mesa.", nameof(tableId));

            return _tables.GetOrAdd(tableId, id => new TableActor(
                id,
                TableRules.VegasStrip,
                _options,
                _hub,
                _wallet,
                _archive,
                _loggerFactory.CreateLogger<TableActor>()));
        }

        public bool TryGet(string tableId, out TableActor? table) => _tables.TryGetValue(tableId, out table);

        public IReadOnlyCollection<string> TableIds => (IReadOnlyCollection<string>)_tables.Keys;

        public async ValueTask DisposeAsync()
        {
            foreach (TableActor table in _tables.Values)
            {
                await table.DisposeAsync().ConfigureAwait(false);
            }

            _tables.Clear();
        }
    }
}
