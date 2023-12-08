namespace ConcurrencyControl;

internal class Program
{
    public static readonly ILoggerFactory MyLoggerFactory
        = LoggerFactory.Create(builder =>
        {
            builder
                .AddFilter("Microsoft", LogLevel.Warning)
                .AddFilter("System", LogLevel.Warning)
                .AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning)
                .AddConsole();
        });

    private static async Task Main()
    {
        MyDbContext.EnsureDatabaseIsCleaned();
        await using (var dbContext = new MyDbContext())
        {
            await dbContext.Database.MigrateAsync();
            if (dbContext.Database.IsSqlite())
            {
                await dbContext.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TRIGGER SetTimestampOnUpdate
                    AFTER UPDATE ON ConcurrentAccountsWithRowVersion
                    BEGIN
                        UPDATE ConcurrentAccountsWithRowVersion
                        SET Timestamp = randomBlob(8)
                        WHERE rowId = NEW.rowId;
                    END
                    """);
                await dbContext.Database.ExecuteSqlRawAsync(
                    """
                    CREATE TRIGGER SetTimestampOnInsert
                    AFTER INSERT ON ConcurrentAccountsWithRowVersion
                    BEGIN
                        UPDATE ConcurrentAccountsWithRowVersion
                        SET Timestamp = randomBlob(8)
                        WHERE rowId = NEW.rowId;
                    END
                    """);
            }
            await dbContext.NonConcurrentAccounts.AddAsync(new NonConcurrentAccount { Id = 1, Balance = 1000.0m });
            await dbContext.ConcurrentAccountsWithToken.AddAsync(new ConcurrentAccountWithToken { Id = 1, Balance = 1000.0m });
            //await dbContext.ConcurrentAccountsWithRowVersion.AddAsync(new ConcurrentAccountWithRowVersion { Id = 1, Balance = 1000.0m });
            await dbContext.SaveChangesAsync();
        }

        Console.WriteLine("========== Concurrency Test with NonConcurrent Account ==============================");
        for (var i = 0; i < 10; i++)
        {
            await TestWithoutConcurrencyControl();
        }

        Console.WriteLine("\n\n========== Concurrency Test with Concurrent Account using Concurrent Token ==========");
        await ConcurrencyControlByConcurrencyToken();

        Console.WriteLine("\n\n========== Concurrency Test with Concurrent Account using Row Version ===============");
        //await ConcurrencyControlByRowVersion();
    }

    private static async Task TestWithoutConcurrencyControl()
    {
        await using (var dbContext = new MyDbContext())
        {
            var account = await dbContext.NonConcurrentAccounts.FindAsync(1);
            ConsoleUtils.WriteInf($"Account Balance (Before): {account!.Balance}");
        }

        var tasks = new List<Task>
        {
            NonConcurrentAccountTask(100),
            NonConcurrentAccountTask(200,false),
            NonConcurrentAccountTask(200),
        };
        await Task.WhenAll(tasks);

        await using (var dbContext = new MyDbContext())
        {
            var account = await dbContext.NonConcurrentAccounts.FindAsync(1);
            ConsoleUtils.WriteInf($"Account Balance (After): {account!.Balance}");
        }

        return;

        static async Task NonConcurrentAccountTask(decimal amount, bool credit = true)
        {
            await using var dbContext = new MyDbContext();
            var account = await dbContext.NonConcurrentAccounts.FindAsync(1);
            if (credit)
            {
                account!.Credit(amount);
            }
            else
            {
                account!.Debit(amount);
            }
            await dbContext.SaveChangesAsync();
        }
    }

    private static async Task ConcurrencyControlByConcurrencyToken()
    {
        await using (var dbContext = new MyDbContext())
        {
            var account = await dbContext.ConcurrentAccountsWithToken.FindAsync(1);
            ConsoleUtils.WriteInf($"Account Balance (Before): {account.Balance}");
        }

        var threads = new Thread[2];
        threads[0] = new Thread(async () =>
        {
            await using var dbContext = new MyDbContext();
            var account = await dbContext.ConcurrentAccountsWithToken.FindAsync(1);
            account.Credit(100);
            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException e)
            {
                ConsoleUtils.WriteErr(e.Message);
            }
        });
        threads[1] = new Thread(async () =>
        {
            await using var dbContext = new MyDbContext();
            var account = await dbContext.ConcurrentAccountsWithToken.FindAsync(1);
            account.Debit(200);
            try
            {
                await dbContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException e)
            {
                ConsoleUtils.WriteErr(e.Message);
            }
        });

        foreach (var t in threads)
        {
            t.Start();
        }

        Thread.Sleep(1000);     // The purpose of this line is merely to display Console output in sequence.
        await using (var dbContext = new MyDbContext())
        {
            var account = await dbContext.ConcurrentAccountsWithToken.FindAsync(1);
            ConsoleUtils.WriteInf($"Account Balance (After): {account.Balance}");
        }
    }

    private static async Task ConcurrencyControlByRowVersion()
    {
        using (var dbContext = new MyDbContext())
        {
            var account = await dbContext.ConcurrentAccountsWithRowVersion.FindAsync(1);
            ConsoleUtils.WriteInf($"Account Balance (Before): {account.Balance}");
        }

        var threads = new Thread[2];
        threads[0] = new Thread(async () =>
        {
            using (var dbContext = new MyDbContext())
            {
                var account = await dbContext.ConcurrentAccountsWithRowVersion.FindAsync(1);
                account.Credit(100);
                try
                {
                    await dbContext.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException e)
                {
                    ConsoleUtils.WriteErr(e.Message);
                }
            }
        });
        threads[1] = new Thread(async () =>
        {
            using (var dbContext = new MyDbContext())
            {
                var account = await dbContext.ConcurrentAccountsWithRowVersion.FindAsync(1);
                account.Debit(200);
                try
                {
                    await dbContext.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException e)
                {
                    ConsoleUtils.WriteErr(e.Message);
                }
            }
        });

        foreach (var t in threads)
        {
            t.Start();
        }

        Thread.Sleep(1000);     // The purpose of this line is merely to display Console output in sequence.
        using (var dbContext = new MyDbContext())
        {
            var account = await dbContext.ConcurrentAccountsWithRowVersion.FindAsync(1);
            ConsoleUtils.WriteInf($"Account Balance (After): {account.Balance}");
        }
    }
}