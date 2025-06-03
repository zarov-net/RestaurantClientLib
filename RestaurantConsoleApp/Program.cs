using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

class Program
{
    private static ILogger<Program> _logger;
    private static IConfiguration _configuration;

    static async Task Main(string[] args)
    {
        // Настройка конфигурации
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        // Настройка логгера
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .AddFile(string.Format(
                    _configuration["Logging:LogFilePath"],
                    DateTime.Now));
        });

        _logger = loggerFactory.CreateLogger<Program>();

        try
        {
            await InitializeDatabase();

            // Используем HTTP клиент (можно заменить на gRPC)
            var client = new RestaurantClient(
                _configuration["ApiSettings:BaseUrl"],
                _configuration["ApiSettings:Username"],
                _configuration["ApiSettings:Password"]);

            // Получаем блюда
            List<Dish> dishes;
            try
            {
                dishes = await client.GetDishesAsync();
                await SaveDishesToDatabase(dishes);

                Console.WriteLine("Список блюд:");
                foreach (var dish in dishes)
                {
                    Console.WriteLine($"{dish.Name} – {dish.Code} – {dish.Price}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                _logger.LogError(ex, "Ошибка при получении блюд");
                return;
            }

            // Создаем заказ
            var order = new Order();
            bool inputValid = false;

            while (!inputValid)
            {
                Console.WriteLine("\nВведите заказ в формате: Код1:Количество1;Код2:Количество2;...");
                var input = Console.ReadLine();

                try
                {
                    ParseOrderInput(input, dishes, order);
                    inputValid = true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка: {ex.Message}");
                    Console.WriteLine("Попробуйте еще раз.");
                }
            }

            // Отправляем заказ
            try
            {
                var success = await client.PlaceOrderAsync(order);
                Console.WriteLine("УСПЕХ");
                _logger.LogInformation("Заказ успешно отправлен");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка: {ex.Message}");
                _logger.LogError(ex, "Ошибка при отправке заказа");
            }
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Критическая ошибка в приложении");
            Console.WriteLine($"Произошла критическая ошибка: {ex.Message}");
        }
    }

    private static async Task InitializeDatabase()
    {
        var connectionString = _configuration.GetConnectionString("PostgreSQL");

        using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();

            // Создаем базу данных, если не существует
            var checkDbCmd = new NpgsqlCommand(
                "SELECT 1 FROM pg_database WHERE datname='restaurant_db'", connection);
            var dbExists = await checkDbCmd.ExecuteScalarAsync() != null;

            if (!dbExists)
            {
                var createDbCmd = new NpgsqlCommand(
                    "CREATE DATABASE restaurant_db", connection);
                await createDbCmd.ExecuteNonQueryAsync();
                _logger.LogInformation("База данных создана");
            }

            // Создаем таблицу dishes, если не существует
            var createTableCmd = new NpgsqlCommand(@"
                CREATE TABLE IF NOT EXISTS dishes (
                    code VARCHAR(50) PRIMARY KEY,
                    name VARCHAR(100) NOT NULL,
                    price DECIMAL(10,2) NOT NULL
                )", connection);

            await createTableCmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Таблица dishes проверена/создана");
        }
    }

    private static async Task SaveDishesToDatabase(List<Dish> dishes)
    {
        var connectionString = _configuration.GetConnectionString("PostgreSQL");

        using (var connection = new NpgsqlConnection(connectionString))
        {
            await connection.OpenAsync();

            // Очищаем таблицу перед вставкой новых данных
            var clearTableCmd = new NpgsqlCommand("TRUNCATE TABLE dishes", connection);
            await clearTableCmd.ExecuteNonQueryAsync();

            // Вставляем данные
            foreach (var dish in dishes)
            {
                var insertCmd = new NpgsqlCommand(
                    "INSERT INTO dishes (code, name, price) VALUES (@code, @name, @price)",
                    connection);

                insertCmd.Parameters.AddWithValue("code", dish.Code);
                insertCmd.Parameters.AddWithValue("name", dish.Name);
                insertCmd.Parameters.AddWithValue("price", dish.Price);

                await insertCmd.ExecuteNonQueryAsync();
            }

            _logger.LogInformation($"Добавлено {dishes.Count} блюд в базу данных");
        }
    }

    private static void ParseOrderInput(string input, List<Dish> dishes, Order order)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new Exception("Ввод не может быть пустым");

        var items = input.Split(';');
        if (items.Length == 0)
            throw new Exception("Неверный формат ввода");

        foreach (var item in items)
        {
            var parts = item.Split(':');
            if (parts.Length != 2)
                throw new Exception($"Неверный формат элемента: {item}");

            var code = parts[0].Trim();
            if (!int.TryParse(parts[1].Trim(), out var quantity) || quantity <= 0)
                throw new Exception($"Некорректное количество для кода {code}");

            if (!dishes.Any(d => d.Code == code))
                throw new Exception($"Блюдо с кодом {code} не найдено");

            order.Items.Add(new OrderItem
            {
                DishCode = code,
                Quantity = quantity
            });
        }
    }
}