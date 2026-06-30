using System.Net.Http;
using System.Net.Http.Json;

namespace GlassLauncher.Services;

// Тонкая обёртка над HttpClient: GET JSON + скачивание файлов.
// Создаётся через IHttpClientFactory (DI), поэтому потокобезопасна.
public class ApiClient
{
    private readonly HttpClient _http;

    public ApiClient(HttpClient http)
    {
        _http = http;
        // User-Agent обязателен для Modrinth/CurseForge
        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
            _http.DefaultRequestHeaders.Add("User-Agent", "GlassLauncher/1.0 (contact@example.com)");
    }

    public Task<T?> GetJsonAsync<T>(string url, CancellationToken ct = default)
        => _http.GetFromJsonAsync<T>(url, ct);

    public HttpClient Raw => _http;

    // Скачивание файла мода/ресурспака на диск с прогрессом.
    public async Task DownloadFileAsync(string url, string destPath,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? -1L;

        await using var src = await resp.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(destPath);

        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            if (total > 0) progress?.Report((double)read / total * 100);
        }
    }
}