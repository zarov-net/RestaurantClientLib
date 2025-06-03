using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using RestaurantClientLib;
using RestaurantClientLib.Grpc;
using RestaurantClientLib.Interfaces;
using RestaurantClientLib.Models;
using Dish = RestaurantClientLib.Grpc.Dish;
using OrderItem = RestaurantClientLib.Grpc.OrderItem;

class Program
{
    private static ILogger<Program> _logger;
    private static IConfiguration _configuration;
    
    static async Task Main(string[] args)
    {
        // Настройка конфигурации и логгера
        ConfigureServices();
        
        try
        {
            await InitializeDatabase();
            
            // Выбираем реализацию клиента (HTTP или gRPC)
            var useGrpc = AskForClientType();
            IRestaurantClient client = CreateClient(useGrpc);
            
            // Получаем список блюд
            var dishes = await GetDishesAsync(client);
            await SaveDishesToDatabase(dishes);
            PrintDishes(dishes);
            
            // Создаем и отправляем заказ
            var order = await CreateOrderAsync(dishes);
            await PlaceOrderAsync(client, order);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Произошла ошибка");
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    }

    private static void ConfigureServices()
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();
            
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder
                .AddConsole()
                .AddFile(string.Format(
                    _configuration["Logging:LogFilePath"], 
                    DateTime.Now));
        });
        
        _logger = loggerFactory.CreateLogger<Program>();
    }

    private static bool AskForClientType()
    {
        Console.WriteLine("Выберите тип клиента (1 - HTTP, 2 - gRPC):");
        var input = Console.ReadLine();
        return input == "2";
    }

    private static IRestaurantClient CreateClient(bool useGrpc)
    {
        if (useGrpc)
        {
            return new RestaurantGrpcClient(_configuration["ApiSettings:GrpcUrl"]);
        }
        else
        {
            return new RestaurantHttpClient(
                _configuration["ApiSettings:BaseUrl"],
                _configuration["ApiSettings:Username"],
                _configuration["ApiSettings:Password"]);
        }
    }

    private static async Task InitializeDatabase()
    {
        var connectionString = _configuration.GetConnectionString("PostgreSQL");
        
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        
        var createTableCmd = new NpgsqlCommand(@"
            CREATE TABLE IF NOT EXISTS dishes (
                code VARCHAR(50) PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                price DECIMAL(10,2) NOT NULL
            )", connection);
            
        await createTableCmd.ExecuteNonQueryAsync();
        _logger.LogInformation("База данных инициализирована");
    }

    private static async Task<Dish[]> GetDishesAsync(IRestaurantClient client)
    {
        try
        {
            var dishes = await client.GetDishesAsync();
            _logger.LogInformation("Получен список блюд");
            return dishes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении блюд");
            throw;
        }
    }

    private static async Task SaveDishesToDatabase(List<Dish> dishes)
    {
        var connectionString = _configuration.GetConnectionString("PostgreSQL");
        
        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        
        // Очищаем таблицу перед вставкой
        await new NpgsqlCommand("TRUNCATE TABLE dishes", connection)
            .ExecuteNonQueryAsync();
        
        // Вставляем данные
        foreach (var dish in dishes)
        {
            var cmd = new NpgsqlCommand(
                "INSERT INTO dishes (code, name, price) VALUES (@code, @name, @price)", 
                connection);
                
            cmd.Parameters.AddWithValue("code", dish.Code);
            cmd.Parameters.AddWithValue("name", dish.Name);
            cmd.Parameters.AddWithValue("price", dish.Price);
            
            await cmd.ExecuteNonQueryAsync();
        }
        
        _logger.LogInformation($"Сохранено {dishes.Count} блюд в БД");
    }

    private static void PrintDishes(Dish[] dishes)
    {
        Console.WriteLine("\nСписок блюд:");
        foreach (var dish in dishes)
        {
            Console.WriteLine($"{dish.Name} – {dish.Code} – {dish.Price}");
        }
    }

    private static async Task<Order> CreateOrderAsync(List<Dish> dishes)
    {
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
            }
        }
        
        return order;
    }

    private static void ParseOrderInput(string input, List<Dish> dishes, Order order)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new Exception("Ввод не может быть пустым");
            
        var items = input.Split(';', StringSplitOptions.RemoveEmptyEntries);
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

    private static async Task PlaceOrderAsync(IRestaurantClient client, Order order)
    {
        try
        {
            await client.PlaceOrderAsync(order);
            Console.WriteLine("УСПЕХ");
            _logger.LogInformation("Заказ успешно отправлен");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
            _logger.LogError(ex, "Ошибка при отправке заказа");
            throw;
        }
    }
}