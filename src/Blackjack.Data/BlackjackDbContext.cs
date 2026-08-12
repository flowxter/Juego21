using System;
using System.Text;
using Blackjack.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Blackjack.Data
{
    public sealed class BlackjackDbContext : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>
    {
        public BlackjackDbContext(DbContextOptions<BlackjackDbContext> options) : base(options)
        {
        }

        public DbSet<Account> Accounts => Set<Account>();

        public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();

        public DbSet<RoundRecord> RoundRecords => Set<RoundRecord>();

        public DbSet<HandRecord> HandRecords => Set<HandRecord>();

        public DbSet<PlayerStats> PlayerStats => Set<PlayerStats>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<AppUser>(entity =>
            {
                entity.Property(u => u.DisplayName).HasMaxLength(40).IsRequired();

                entity.HasOne(u => u.Account)
                    .WithOne(a => a.User)
                    .HasForeignKey<Account>(a => a.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(u => u.Stats)
                    .WithOne(s => s.User)
                    .HasForeignKey<PlayerStats>(s => s.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<Account>(entity =>
            {
                entity.HasKey(a => a.Id);
                entity.HasIndex(a => a.UserId).IsUnique();
                entity.Property(a => a.Balance).HasPrecision(18, 2);
            });

            builder.Entity<LedgerTransaction>(entity =>
            {
                entity.HasKey(t => t.Id);
                entity.Property(t => t.Amount).HasPrecision(18, 2);
                entity.Property(t => t.BalanceAfter).HasPrecision(18, 2);
                entity.Property(t => t.RoundId).HasMaxLength(32);

                // El extracto de un jugador siempre se lee del más reciente
                // hacia atrás; este índice es el que lo hace barato.
                entity.HasIndex(t => new { t.UserId, t.CreatedUtc });

                entity.HasOne(t => t.User)
                    .WithMany()
                    .HasForeignKey(t => t.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<RoundRecord>(entity =>
            {
                entity.HasKey(r => r.Id);
                entity.Property(r => r.RoundId).HasMaxLength(32).IsRequired();
                entity.Property(r => r.TableId).HasMaxLength(64).IsRequired();
                entity.Property(r => r.DealerCards).HasMaxLength(64);

                entity.Property(r => r.MainBet).HasPrecision(18, 2);
                entity.Property(r => r.PerfectPairsBet).HasPrecision(18, 2);
                entity.Property(r => r.TwentyOnePlus3Bet).HasPrecision(18, 2);
                entity.Property(r => r.InsuranceBet).HasPrecision(18, 2);
                entity.Property(r => r.TotalReturned).HasPrecision(18, 2);
                entity.Property(r => r.NetProfit).HasPrecision(18, 2);

                entity.HasIndex(r => new { r.UserId, r.PlayedUtc });
                entity.HasIndex(r => r.RoundId);

                entity.HasOne(r => r.User)
                    .WithMany()
                    .HasForeignKey(r => r.UserId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasMany(r => r.Hands)
                    .WithOne(h => h.Round)
                    .HasForeignKey(h => h.RoundRecordId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<HandRecord>(entity =>
            {
                entity.HasKey(h => h.Id);
                entity.Property(h => h.Cards).HasMaxLength(64);
                entity.Property(h => h.Bet).HasPrecision(18, 2);
                entity.Property(h => h.Returned).HasPrecision(18, 2);
            });

            builder.Entity<PlayerStats>(entity =>
            {
                entity.HasKey(s => s.UserId);
                entity.Property(s => s.TotalWagered).HasPrecision(18, 2);
                entity.Property(s => s.TotalReturned).HasPrecision(18, 2);
                entity.Property(s => s.BiggestWin).HasPrecision(18, 2);

                // NetResult se calcula en memoria; no es una columna.
                entity.Ignore(s => s.NetResult);
            });

            ApplySnakeCaseNames(builder);
        }

        /// <summary>
        /// Pasa tablas y columnas a snake_case.
        ///
        /// Es la convención de Postgres, y además evita tener que entrecomillar
        /// identificadores en el SQL directo que usa el monedero: sin esto
        /// habría que escribir UPDATE "Accounts" SET "Balance" en vez de algo
        /// legible.
        /// </summary>
        private static void ApplySnakeCaseNames(ModelBuilder builder)
        {
            foreach (IMutableEntityType entity in builder.Model.GetEntityTypes())
            {
                string? table = entity.GetTableName();
                if (table != null) entity.SetTableName(ToSnakeCase(table));

                foreach (IMutableProperty property in entity.GetProperties())
                {
                    property.SetColumnName(ToSnakeCase(property.Name));
                }

                foreach (IMutableKey key in entity.GetKeys())
                {
                    string? name = key.GetName();
                    if (name != null) key.SetName(ToSnakeCase(name));
                }

                foreach (IMutableForeignKey fk in entity.GetForeignKeys())
                {
                    string? name = fk.GetConstraintName();
                    if (name != null) fk.SetConstraintName(ToSnakeCase(name));
                }

                foreach (IMutableIndex index in entity.GetIndexes())
                {
                    string? name = index.GetDatabaseName();
                    if (name != null) index.SetDatabaseName(ToSnakeCase(name));
                }
            }
        }

        private static string ToSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            var sb = new StringBuilder(value.Length + 8);

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];

                if (char.IsUpper(c))
                {
                    if (i > 0 && value[i - 1] != '_') sb.Append('_');
                    sb.Append(char.ToLowerInvariant(c));
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToString();
        }
    }
}
