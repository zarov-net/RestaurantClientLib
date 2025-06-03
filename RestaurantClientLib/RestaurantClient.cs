using Newtonsoft.Json;
using RestaurantClientLib.Models;
using System.Net.Http.Headers;
using System.Text;

public class RestaurantClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _username;
    private readonly string _password;

    public RestaurantClient(string baseUrl, string username, string password)
    {
        _baseUrl = baseUrl;
        _username = username;
        _password = password;
        _httpClient = new HttpClient();
    }

    private async Task<ApiResponse<T>> SendRequestAsync<T>(HttpMethod method, string endpoint, object content = null)
    {
        var request = new HttpRequestMessage(method, $"{_baseUrl}/{endpoint}");

        // Basic Auth
        var authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_password}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authHeader);

        if (content != null)
        {
            var jsonContent = JsonConvert.SerializeObject(content);
            request.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
        }

        var response = await _httpClient.SendAsync(request);
        var responseContent = await response.Content.ReadAsStringAsync();

        return JsonConvert.DeserializeObject<ApiResponse<T>>(responseContent);
    }

    public async Task<List<Dish>> GetDishesAsync()
    {
        var response = await SendRequestAsync<List<Dish>>(HttpMethod.Get, "dishes");

        if (!response.Success)
        {
            throw new Exception(response.ErrorMessage);
        }

        return response.Data;
    }

    public async Task<bool> PlaceOrderAsync(Order order)
    {
        var response = await SendRequestAsync<object>(HttpMethod.Post, "orders", order);

        if (!response.Success)
        {
            throw new Exception(response.ErrorMessage);
        }

        return true;
    }

}