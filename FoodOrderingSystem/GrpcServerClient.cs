using FoodOrderingGrpcService;
using Google.Protobuf.WellKnownTypes;
using Grpc.Net.Client;

namespace FoodOrderingSystem;

public class GrpcServerClient : IDisposable
{
    private readonly GrpcChannel _channel;
    private readonly GrpcApiService.GrpcApiServiceClient _client;

    public GrpcServerClient(string address)
    {
        _channel = GrpcChannel.ForAddress(address);
        _client = new GrpcApiService.GrpcApiServiceClient(_channel);
    }

    public async Task<List<MenuItem>> GetDishesAsync()
    {
        var response = await _client.GetMenuAsync(new BoolValue { Value = true });
        
        if (!response.Success)
        {
            throw new Exception(response.ErrorMessage);
        }

        return response.MenuItems.Select(mi => new MenuItem
        {
            Id = mi.Id,
            Article = mi.Article,
            Name = mi.Name,
            Price = mi.Price,
            IsWeighted = mi.IsWeighted,
            FullPath = mi.FullPath,
            Barcodes = { mi.Barcodes }
        }).ToList();
    }

    public async Task<bool> SendOrderAsync(Order order)
    {
        var grpcOrder = new Order
        {
            Id = order.Id
        };
        
        grpcOrder.OrderItems.AddRange(order.OrderItems.Select(item => new OrderItem
        {
            Id = item.Id,
            Quantity = item.Quantity
        }));

        var response = await _client.SendOrderAsync(grpcOrder);
        
        if (!response.Success)
        {
            throw new Exception(response.ErrorMessage ?? "Unknown error");
        }

        return response.Success;
    }

    public void Dispose()
    {
        _channel.Dispose();
        GC.SuppressFinalize(this);
    }
}