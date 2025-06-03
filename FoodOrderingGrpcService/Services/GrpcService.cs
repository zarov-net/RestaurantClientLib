using Dapper;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Npgsql;
using Microsoft.Extensions.Options;
using FoodOrderingGrpcService;
using Grpc.Net.Client;

namespace FoodOrderingGrpcService.Services;

public class GrpcService : GrpcApiService.GrpcApiServiceBase, IDisposable
{
    private readonly string _connectionString;
    private readonly GrpcChannel? _channel;

    public GrpcService(IOptions<GrpcSettings> options, IConfiguration configuration)
    {
        _connectionString = "Host=localhost;Port=5432;Database=mydb;Username=user;Password=g#123JSC*";
        
        // Для внутренних вызовов других сервисов
        if (!string.IsNullOrEmpty(options.Value.ServerAddress))
        {
            _channel = GrpcChannel.ForAddress(options.Value.ServerAddress);
        }
    }

    public override async Task<GetMenuResponse> GetMenu(BoolValue request, ServerCallContext context)
    {
        try
        {
            var menuItems = await GetMenuFromDatabaseAsync();
            
            return new GetMenuResponse 
            {
                Success = true,
                MenuItems = { menuItems }
            };
        }
        catch (Exception ex)
        {
            return new GetMenuResponse 
            {
                Success = false,
                ErrorMessage = $"Ошибка при получении меню: {ex.Message}"
            };
        }
    }

    public override async Task<SendOrderResponse> SendOrder(Order request, ServerCallContext context)
    {
        try
        {
            var orderId = await SaveOrderToDatabaseAsync(request);
            
            return new SendOrderResponse 
            {
                Success = !string.IsNullOrEmpty(orderId),
                ErrorMessage = string.IsNullOrEmpty(orderId) ? "Не удалось сохранить заказ" : string.Empty
            };
        }
        catch (Exception ex)
        {
            return new SendOrderResponse 
            {
                Success = false,
                ErrorMessage = $"Ошибка при обработке заказа: {ex.Message}"
            };
        }
    }

    private async Task<List<MenuItem>> GetMenuFromDatabaseAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        
        const string query = @"
            SELECT 
                id AS Id,
                article AS Article,
                name AS Name,
                price AS Price,
                fullpath AS FullPath
            FROM dishes";
        
        var menuItems = await connection.QueryAsync<MenuItem>(query);
        return menuItems.AsList();
    }

    private async Task<string?> SaveOrderToDatabaseAsync(Order order)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Сохраняем заказ
            const string orderQuery = @"
        INSERT INTO orders (id)
        VALUES (@Id)
        ON CONFLICT (id) DO NOTHING
        RETURNING id";
    
            var orderId = await connection.ExecuteScalarAsync<string>(orderQuery, new { order.Id }, transaction);
    
            if (string.IsNullOrEmpty(orderId))
            {
                // Если заказ уже существует, используем существующий ID
                orderId = order.Id;
            }
    
            // 2. Удаляем старые позиции заказа (если они есть)
            const string deleteQuery = "DELETE FROM orderitems WHERE orderid = @OrderId";
            await connection.ExecuteAsync(deleteQuery, new { OrderId = orderId }, transaction);
    
            // 3. Добавляем новые позиции заказа
            const string itemsQuery = @"
        INSERT INTO orderitems (orderid, dishid, quantity)
        VALUES (@OrderId, @DishId, @Quantity)";
    
            var orderItems = order.OrderItems.Select(item => new 
            {
                OrderId = orderId,
                DishId = item.Id,
                item.Quantity
            });
    
            await connection.ExecuteAsync(itemsQuery, orderItems, transaction);
    
            await transaction.CommitAsync();
            return orderId;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            throw new Exception($"Ошибка сохранения заказа: {ex.Message}", ex);
        }
    }


    public void Dispose()
    {
        _channel?.Dispose();
        GC.SuppressFinalize(this);
    }
}