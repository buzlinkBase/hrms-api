using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace Buzlink.HR.UI;

internal static class HttpService
{
    private static readonly HttpClient client = new HttpClient(new HttpClientHandler
    {
        AllowAutoRedirect = false,
    });
    private static string _baseUrl = "http://167.99.67.128:8081/api/v1/";
    public static void SetBaseUrl(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    public static async Task<string> GetAsync(string url)
    {
        url = string.Concat(_baseUrl, url);
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public static async Task<string> PostAsync<T>(string url, T data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        url = string.Concat(_baseUrl, url);
        //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        var response = await client.PostAsync(new Uri(url), content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public static async Task<string> PutAsync<T>(string url, T data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        url = string.Concat(_baseUrl, url);
        var response = await client.PutAsync(url, content);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public static async Task<string> DeleteAsync(string url)
    {
        url = string.Concat(_baseUrl, url);
        var response = await client.DeleteAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    public static async Task<string> SendCustomVerbAsync<T>(string url, string verb, T data)
    {
        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        url = string.Concat(_baseUrl, url);
        var request = new HttpRequestMessage(new HttpMethod(verb), url) { Content = content };
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
}
public class ResponseModel<T>
{
    public string? Message { get; set; } = string.Empty;
    public int? StatusCode { get; set; } = 200;
    public T? Data { get; set; }
}