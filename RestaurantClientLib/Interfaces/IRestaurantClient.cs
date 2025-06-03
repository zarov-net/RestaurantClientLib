using RestaurantClientLib.Models;

namespace RestaurantClientLib.Interfaces;

public interface IRestaurantClient
{
    Task<Dish[]> GetDishesAsync(CancellationToken cancellationToken = default);
    Task<bool> SendOrderAsync(Order order, CancellationToken cancellationToken = default);
}