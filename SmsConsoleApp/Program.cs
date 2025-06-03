using System.Globalization;
using FoodOrderingGrpcService;
using FoodOrderingSystem;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Serilog;

class Program
{
    private static IConfigurationRoot Configuration;
    private static ILogger Logger;

    static async Task Main(string[] args)
    {
        // Настройка конфигурации
        Configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Настройка логирования
        Logger = new LoggerConfiguration()
            .WriteTo.File(
                path: $"test-sms-console-app-{DateTime.Now:yyyyMMdd}.log",
                rollingInterval: RollingInterval.Day,
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Logger.Information("Приложение запущено");

        try
        {
            string connectionString = Configuration.GetConnectionString("DefaultConnection");
            InitializeDatabase(connectionString);
            AddSampleDishesOnce(connectionString);
            var grpcAddress = Configuration["GrpcServer:Address"];
            var grpcClient = new GrpcServerClient(grpcAddress);

            List<MenuItem> dishes;
            try
            {
                dishes = await grpcClient.GetDishesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка получения меню: {ex.Message}");
                Logger.Error(ex, "Ошибка получения меню");
                return;
            }

            // Запись в БД и вывод в консоль
            SaveDishesToDatabase(connectionString, dishes);
            
            Console.WriteLine("Список блюд:");
            foreach (var dish in dishes)
            {
                Console.WriteLine($"{dish.Name} – {dish.Article} – {dish.Price}");
            }

            // Создаём заказ
            var order = new Order
            {
                Id = Guid.NewGuid().ToString() // Имя свойства из proto — Id, не OrderId
            };

            while (true)
            {
                Console.WriteLine("Введите заказ в формате Код1:Количество1;Код2:Количество2;...");
                var input = Console.ReadLine();

                if (TryParseOrderInput(input, dishes, out var orderItems, out string error))
                {
                    order.OrderItems.Clear();
                    foreach (var item in orderItems)
                    {
                        order.OrderItems.Add(item);
                    }
                    break;
                }
                else
                {
                    Console.WriteLine($"Ошибка ввода: {error}");
                }
            }

            try
            {
                bool success = await grpcClient.SendOrderAsync(order);
                Console.WriteLine(success ? "УСПЕХ" : "Ошибка при отправке заказа");
                Logger.Information(success ? "Заказ успешно отправлен" : "Ошибка при отправке заказа");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при отправке заказа: {ex.Message}");
                Logger.Error(ex, "Ошибка при отправке заказа");
            }
        }
        catch (Exception ex)
        {
            Logger.Fatal(ex, "Необработанная ошибка");
            Console.WriteLine($"Фатальная ошибка: {ex.Message}");
        }
        finally
        {
            Logger.Information("Приложение завершено");
            Log.CloseAndFlush();
        }
    }

    private static void InitializeDatabase(string connectionString)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        var createTableQuery = @"
        CREATE TABLE IF NOT EXISTS Dishes (
            Id TEXT PRIMARY KEY,
            Article TEXT,
            Name TEXT,
            Price DOUBLE PRECISION,
            IsWeighted BOOLEAN,
            FullPath TEXT
        );

        CREATE TABLE IF NOT EXISTS Orders (
            Id TEXT PRIMARY KEY,
            CreatedAt TIMESTAMP NOT NULL DEFAULT NOW()
        );

        CREATE TABLE IF NOT EXISTS OrderItems (
            Id SERIAL PRIMARY KEY,
            OrderId TEXT NOT NULL REFERENCES Orders(Id) ON DELETE CASCADE,
            DishId TEXT NOT NULL REFERENCES Dishes(Id),
            Quantity DOUBLE PRECISION NOT NULL CHECK (Quantity > 0)
        );";

        using var command = new NpgsqlCommand(createTableQuery, connection);
        command.ExecuteNonQuery();

        Logger.Information("База данных и таблицы инициализированы");
    }


    private static void SaveDishesToDatabase(string connectionString, List<MenuItem> dishes)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();

        foreach (var dish in dishes)
        {
            var query = @"
                INSERT INTO Dishes (Id, Article, Name, Price, IsWeighted, FullPath)
                VALUES (@Id, @Article, @Name, @Price, @IsWeighted, @FullPath)
                ON CONFLICT (Id) DO UPDATE SET
                    Article = EXCLUDED.Article,
                    Name = EXCLUDED.Name,
                    Price = EXCLUDED.Price,
                    IsWeighted = EXCLUDED.IsWeighted,
                    FullPath = EXCLUDED.FullPath;";

            using var cmd = new NpgsqlCommand(query, connection);
            cmd.Parameters.AddWithValue("Id", dish.Id);
            cmd.Parameters.AddWithValue("Article", dish.Article);
            cmd.Parameters.AddWithValue("Name", dish.Name);
            cmd.Parameters.AddWithValue("Price", dish.Price);
            cmd.Parameters.AddWithValue("IsWeighted", dish.IsWeighted);
            cmd.Parameters.AddWithValue("FullPath", dish.FullPath);
            cmd.ExecuteNonQuery();
        }

        Logger.Information("Данные блюд сохранены в базу данных");
    }
    
    private static void AddSampleDishesOnce(string connectionString)
{
    using var connection = new NpgsqlConnection(connectionString);
    connection.Open();

    // Проверим, есть ли уже блюда в таблице
    using (var checkCmd = new NpgsqlCommand("SELECT COUNT(*) FROM Dishes;", connection))
    {
        var count = (long)checkCmd.ExecuteScalar();
        if (count >= 10)
        {
            Logger.Information("В таблице Dishes уже есть 10 и более записей, добавление пропущено.");
            return;
        }
    }

    // Создаём 10 тестовых блюд
    var sampleDishes = new List<MenuItem>
    {
        new MenuItem { Id = Guid.NewGuid().ToString(), Article = "A01", Name = "Салат Цезарь", Price = 250, IsWeighted = false, FullPath = "Салаты/Цезарь" },
        new MenuItem { Id = Guid.NewGuid().ToString(), Article = "A02", Name = "Суп Борщ", Price = 150, IsWeighted = true, FullPath = "Супы/Борщ" },
        new MenuItem { Id = Guid.NewGuid().ToString(), Article = "A03", Name = "Пицца Маргарита", Price = 500, IsWeighted = false, FullPath = "Пицца/Маргарита" },
        new MenuItem { Id = Guid.NewGuid().ToString(), Article = "A04", Name = "Котлета по-киевски", Price = 300, IsWeighted = false, FullPath = "Горячие блюда/Котлета" },
        new MenuItem { Id = Guid.NewGuid().ToString(), Article = "A05", Name = "Компот из сухофруктов", Price = 100, IsWeighted = false, FullPath = "Напитки/Компот" },
        new MenuItem { Id = Guid.NewGuid().ToString(), Article = "A06", Name = "Картофель фри", Price = 120, IsWeighted = true, FullPath = "Гарниры/Картофель" },
        new MenuItem { Id = Guid.NewGuid().ToString(), Article = "A07", Name = "Оливье", Price = 200, IsWeighted = true, FullPath = "Салаты/Оливье" },
        new MenuItem { Id = Guid.NewGuid().ToString(), Article = "A08", Name = "Чай черный", Price = 80, IsWeighted = false, FullPath = "Напитки/Чай" },
        new MenuItem { Id = Guid.NewGuid().ToString(), Article = "A09", Name = "Блинчики с творогом", Price = 180, IsWeighted = false, FullPath = "Десерты/Блинчики" },
        new MenuItem { Id = Guid.NewGuid().ToString(), Article = "A10", Name = "Кофе эспрессо", Price = 150, IsWeighted = false, FullPath = "Напитки/Кофе" },
    };

    foreach (var dish in sampleDishes)
    {
        var query = @"
            INSERT INTO Dishes (Id, Article, Name, Price, IsWeighted, FullPath)
            VALUES (@Id, @Article, @Name, @Price, @IsWeighted, @FullPath)
            ON CONFLICT (Id) DO NOTHING;";

        using var cmd = new NpgsqlCommand(query, connection);
        cmd.Parameters.AddWithValue("Id", dish.Id);
        cmd.Parameters.AddWithValue("Article", dish.Article);
        cmd.Parameters.AddWithValue("Name", dish.Name);
        cmd.Parameters.AddWithValue("Price", dish.Price);
        cmd.Parameters.AddWithValue("IsWeighted", dish.IsWeighted);
        cmd.Parameters.AddWithValue("FullPath", dish.FullPath ?? (object)DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    Logger.Information("Добавлено 10 тестовых блюд в базу данных");
}


    private static bool TryParseOrderInput(string input, List<MenuItem> dishes, out List<OrderItem> orderItems, out string error)
    {
        orderItems = new List<OrderItem>();
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "Пустой ввод";
            return false;
        }

        var parts = input.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var pair = part.Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (pair.Length != 2)
            {
                error = $"Неверный формат пары: '{part}'. Ожидается формат 'Код:Количество'";
                return false;
            }

            var code = pair[0].Trim();
            var quantityStr = pair[1].Trim();

            if (string.IsNullOrEmpty(code))
            {
                error = $"Пустой код блюда в паре: '{part}'";
                return false;
            }

            if (!double.TryParse(quantityStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double quantity) || quantity <= 0)
            {
                error = $"Некорректное количество '{quantityStr}' для кода '{code}'. Должно быть положительным числом";
                return false;
            }

            var dish = dishes.Find(d => d.Article.Equals(code, StringComparison.OrdinalIgnoreCase));
            if (dish == null)
            {
                var availableCodes = string.Join(", ", dishes.Select(d => d.Article));
                error = $"Код блюда '{code}' не найден. Доступные коды: {availableCodes}";
                return false;
            }

            orderItems.Add(new OrderItem 
            { 
                Id = dish.Id, 
                Quantity = quantity 
            });
        }

        if (orderItems.Count == 0)
        {
            error = "Не указано ни одного блюда для заказа";
            return false;
        }

        return true;
    }
}
