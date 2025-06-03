using Grpc.Net.Client;
using RestaurantClientLib.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RestaurantClientLib
{
    public class GrpcRestaurantClient
    {
        private readonly RestaurantClient _client;

        public GrpcRestaurantClient(string serverUrl)
        {
            var channel = GrpcChannel.ForAddress(serverUrl);
            _client = new RestaurantClient(channel);
        }

        public async Task<List<Dish>> GetDishesAsync()
        {
            var response = await _client.GetDishesAsync(new GetDishesRequest());

            if (!response.Success)
            {
                throw new Exception(response.ErrorMessage);
            }

            var dishes = new List<Dish>();
            foreach (var dish in response.Dishes)
            {
                dishes.Add(new Dish
                {
                    Code = dish.Code,
                    Name = dish.Name,
                    Price = (decimal)dish.Price
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
            {
                throw new Exception(response.ErrorMessage);
            }

            return true;
        }
    }
}
