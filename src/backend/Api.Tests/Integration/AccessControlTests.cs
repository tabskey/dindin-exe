using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Api.Tests.Integration;

public partial class AccessControlTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AccessControlTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Balance_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.Client.GetAsync("/accounts/1/balance");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Movement_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.Client.PostAsJsonAsync("/accounts/1/movements", new { type = 0, amount = 10 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task History_WithoutToken_ReturnsUnauthorized()
    {
        var response = await _factory.Client.GetAsync("/accounts/1/movements");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Balance_WithOtherAccountToken_ReturnsForbidden()
    {
        var (anaId, _, anaToken) = await _factory.RegisterAsync("Ana");
        var (brunoId, _, _) = await _factory.RegisterAsync("Bruno");

        var response = await _factory.GetAsync($"/accounts/{brunoId}/balance", anaToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Movement_WithOtherAccountToken_ReturnsForbidden()
    {
        var (_, _, anaToken) = await _factory.RegisterAsync("Ana");
        var (brunoId, _, _) = await _factory.RegisterAsync("Bruno");

        var response = await _factory.PostAsync($"/accounts/{brunoId}/movements",
            new { type = 0, amount = 10 }, anaToken, "forbidden-move");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Avatar_UploadThenDownload_ReturnsNoContentThenFile()
    {
        var (id, _, token) = await _factory.RegisterAsync("Com Avatar");
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

        var upload = await UploadAvatarAsync(id, token, bytes, "image/png");

        Assert.Equal(HttpStatusCode.NoContent, upload.StatusCode);

        var download = await _factory.GetAsync($"/accounts/{id}/avatar", token);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Avatar_InvalidContentType_ReturnsBadRequest()
    {
        var (id, _, token) = await _factory.RegisterAsync("Sem Avatar");

        var response = await UploadAvatarAsync(id, token, new byte[] { 1, 2, 3 }, "text/plain");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Avatar_TooLarge_ReturnsBadRequest()
    {
        var (id, _, token) = await _factory.RegisterAsync("Grande");

        var response = await UploadAvatarAsync(id, token, new byte[512 * 1024 + 1], "image/png");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Avatar_Missing_ReturnsNotFound()
    {
        var (id, _, token) = await _factory.RegisterAsync("Sem Avatar");

        var response = await _factory.GetAsync($"/accounts/{id}/avatar", token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<HttpResponseMessage> UploadAvatarAsync(long accountId, string token, byte[] bytes, string contentType)
    {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(fileContent, "file", "avatar.png");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"/accounts/{accountId}/avatar")
        {
            Content = form
        };
        request.Headers.Add("Authorization", $"Bearer {token}");
        return await _factory.Client.SendAsync(request);
    }
}
