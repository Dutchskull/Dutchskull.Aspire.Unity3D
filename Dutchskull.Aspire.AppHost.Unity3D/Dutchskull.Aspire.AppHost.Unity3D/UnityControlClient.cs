using System.Text;
using System.Text.Json;

namespace Dutchskull.Aspire.Unity3D.Hosting;

public sealed class UnityControlClient : IDisposable
{
    private readonly HttpClient _http;

    public UnityControlClient(HttpClient? httpClient = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    public async Task<bool> IsHealthyAsync(Uri baseUrl, CancellationToken cancellationToken = default)
    {
        Uri url = new(baseUrl, "health");

        using HttpResponseMessage res = await _http.GetAsync(url, cancellationToken).ConfigureAwait(false);
        return res.IsSuccessStatusCode;
    }

    public async Task<bool> StartProjectAsync(Uri baseUrl, string? scene = null, CancellationToken cancellationToken = default)
    {
        string sceneValue = scene ?? "-1";
        string relativePath = $"start/{Uri.EscapeDataString(sceneValue)}";
        Uri url = new(baseUrl, relativePath);
        StringContent content = new(string.Empty, Encoding.UTF8, "application/json");
        try
        {
            using HttpResponseMessage response = await _http.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> StopProjectAsync(Uri baseUrl, CancellationToken cancellationToken = default)
    {
        Uri url = new(baseUrl, "stop");
        StringContent content = new(JsonSerializer.Serialize(new { }), Encoding.UTF8, "application/json");
        try
        {
            using HttpResponseMessage res = await _http.PostAsync(url, content, cancellationToken).ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public void Dispose() => _http.Dispose();
}