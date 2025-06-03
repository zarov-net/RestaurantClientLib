using Grpc.Net.Client;
using RestaurantClientLib.Models;

namespace RestaurantClientLib.Grpc;

public class RestaurantGrpcClient
{
    private readonly Restaurant.RestaurantClient _client;

    public RestaurantGrpcClient(string serverUrl)
    {
        var channel = GrpcChannel.ForAddress(serverUrl);
        _client = new Restaurant.RestaurantClient(channel);
    }

    public async Task<List<Dish>> GetDishesAsync()
    {
        var response = await _client.GetDishesAsync(new GetDishesRequest());
        
        if (!response.Success)
            throw new Exception(response.ErrorMessage);

        var dishes = new List<Dish>();
        foreach (var dish in response.Dishes)
        {
            dishes.Add(new Dish
            {
                Code = dish.Code,
                Name = dish.Name,
                Price = dish.Price
            });
        }

        return dishes;
    }

    public async Task<bool> PlaceOrderAsync(Order order)
    {
        var request = new PlaceOrderRequest();
        foreach (var item in order.Items)
        {
            request.Items.Add(new OrderItem
            {
                DishCode = item.DishCode,
                Quantity = item.Quantity
            });
        }

        var response = await _client.PlaceOrderAsync(request);
        
        if (!response.Success)
            throw new Exception(response.ErrorMessage);

        return true;
    }
}