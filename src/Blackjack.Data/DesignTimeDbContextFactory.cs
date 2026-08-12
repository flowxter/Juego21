using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Blackjack.Data
{
    /// <summary>
    /// Construye el contexto para las herramientas de EF Core.
    ///
    /// Sin esto, 'dotnet ef migrations add' arrancaría el servidor entero para
    /// obtener el contexto, y el servidor intenta conectar y migrar al
    /// levantarse: no se podría generar una migración sin tener ya la base en
    /// marcha, que es justo lo contrario de lo que hace falta.
    /// </summary>
    public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<BlackjackDbContext>
    {
        public BlackjackDbContext CreateDbContext(string[] args)
        {
            string connection = Environment.GetEnvironmentVariable("BLACKJACK_CONNECTION")
                ?? "Host=localhost;Port=5433;Database=blackjack;Username=blackjack;Password=blackjack_dev";

            var options = new DbContextOptionsBuilder<BlackjackDbContext>()
                .UseNpgsql(connection)
                .Options;

            return new BlackjackDbContext(options);
        }
    }
}
