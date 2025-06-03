using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RestaurantClientLib.Interfaces;
using RestaurantClientLib.Models;

namespace RestaurantClientLib;

public class RestaurantHttpClient : IRestaurantClient
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;

    public RestaurantHttpClient(string endpoint, string username, string password)
    {
        _endpoint = endpoint;
        _httpClient = new HttpClient();
        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
    }

    public async Task<Dish[]> GetDishesAsync(CancellationToken cancellationToken = default)
    {
        var requestBody = new { RequestType = "Request1" }; 
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_endpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var resp = JsonSerializer.Deserialize<ApiResponse<Dish[]>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (resp == null || !resp.Success)
            throw new InvalidOperationException(resp?.ErrorMessage ?? "Unknown error");

        return resp.Data!;
    }

    public async Task<bool> SendOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        var requestBody = new { RequestType = "Request2", Order = order };
        var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(_endpoint, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var resp = JsonSerializer.Deserialize<ApiResponse<object>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (resp == null)
            throw new InvalidOperationException("Invalid response");

        if (!resp.Success)
            throw new InvalidOperationException(resp.ErrorMessage ?? "Unknown error");

        return true;
    }
}