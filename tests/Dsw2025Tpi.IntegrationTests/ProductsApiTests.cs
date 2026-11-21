using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.Application.Dtos.Products;
using Dsw2025Tpi.Data.Identity;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dsw2025Tpi.IntegrationTests;

[Collection("IntegrationTests")]
public sealed class ProductsApiTests(CustomWebApplicationFactory factory)
    : ApiIntegrationTestBase(factory)
{
    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        using var response = await Client.PostAsJsonAsync("/api/products", IphoneRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await WithDatabaseAsync(async db => Assert.Empty(await db.Products.ToListAsync()));
    }

    [Fact]
    public async Task Create_AsAdministrator_ReturnsCreatedAndPersistsProduct()
    {
        AuthenticateAs(AppRoles.Administrador);
        var request = IphoneRequest();

        using var response = await Client.PostAsJsonAsync("/api/products", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = Assert.IsType<ProductResponse>(await response.Content.ReadFromJsonAsync<ProductResponse>());
        Assert.NotEqual(Guid.Empty, created.Id);
        await WithDatabaseAsync(async db =>
        {
            var saved = await db.Products.SingleAsync(p => p.Id == created.Id);
            Assert.Equal(request.Sku, saved.Sku);
            Assert.Equal(request.InternalCode, saved.InternalCode);
            Assert.Equal(request.Name, saved.Name);
            Assert.Equal(request.Description, saved.Description);
            Assert.Equal(request.CurrentUnitPrice, saved.CurrentUnitPrice);
            Assert.Equal(request.StockQuantity, saved.StockQuantity);
            Assert.Equal(request.ImageUrl, saved.ImageUrl);
            Assert.True(saved.IsActive);
            Assert.Equal(new ProductResponse(saved.Id, saved.Sku, saved.InternalCode, saved.Name,
                saved.Description, saved.CurrentUnitPrice, saved.StockQuantity, saved.ImageUrl,
                saved.IsActive), created);
        });
    }

    [Fact]
    public async Task Create_WithDuplicateSku_ReturnsClientErrorAndLeavesOriginalProduct()
    {
        var original = Product.Create("1001", "2002", "Apple AirPods", "", 150m, 3);
        await SeedProductsAsync(original);
        AuthenticateAs(AppRoles.Administrador);

        using var response = await Client.PostAsJsonAsync("/api/products", IphoneRequest());

        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.Conflict });
        var error = Assert.IsType<ApiError>(await response.Content.ReadFromJsonAsync<ApiError>());
        Assert.Equal("PRODUCT_SKU_ALREADY_EXIST}S", error.Code);
        Assert.Contains(original.Sku, error.Message);
        await WithDatabaseAsync(async db =>
        {
            var saved = await db.Products.SingleAsync();
            Assert.Equal(original.Id, saved.Id);
            Assert.Equal(original.Name, saved.Name);
        });
    }

    [Fact]
    public async Task List_WithoutToken_ReturnsPagedJson()
    {
        var iphone = Product.Create("1001", "2001", "Apple iPhone", "", 800m, 10);
        await SeedProductsAsync(iphone);

        var page = await GetPageAsync<ProductResponse>("/api/products");

        Assert.Equal(iphone.Id, Assert.Single(page.Items).Id);
        Assert.Equal(1, page.TotalCount);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(10, page.PageSize);
        Assert.Equal(1, page.TotalPages);
    }

    [Fact]
    public async Task List_WithSearchAndPagination_ReturnsRequestedPageOfMatches()
    {
        var iphone = Product.Create("1001", "2001", "Apple iPhone", "", 800m, 10);
        var iphonePro = Product.Create("1002", "2002", "Apple iPhone Pro", "", 999m, 10);
        var airpods = Product.Create("1003", "2003", "Apple AirPods", "", 150m, 10);
        await SeedProductsAsync(airpods, iphonePro, iphone);

        var page = await GetPageAsync<ProductResponse>("/api/products?search=iPhone&pageNumber=2&pageSize=1");

        Assert.Equal(iphonePro.Id, Assert.Single(page.Items).Id);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(1, page.PageSize);
        Assert.Equal(2, page.TotalPages);
    }

    [Fact]
    public async Task ListAdmin_WithoutStatusFilter_IncludesProductExcludedFromPublicCatalog()
    {
        var iphone = Product.Create("1001", "2001", "Apple iPhone", "", 800m, 10);
        var airpods = Product.Create("1002", "2002", "Apple AirPods", "", 150m, 10);
        airpods.Deactivate();
        await SeedProductsAsync(iphone, airpods);

        var publicPage = await GetPageAsync<ProductResponse>("/api/products");
        AuthenticateAs(AppRoles.Administrador);
        var adminPage = await GetPageAsync<ProductListItemDto>("/api/products/admin");

        Assert.Equal(iphone.Id, Assert.Single(publicPage.Items).Id);
        Assert.Equal(1, publicPage.TotalCount);
        Assert.Equal(2, adminPage.TotalCount);
        Assert.Contains(adminPage.Items, p => p.Id == iphone.Id && p.IsActive);
        Assert.Contains(adminPage.Items, p => p.Id == airpods.Id && !p.IsActive);
    }

    private static ProductRequest IphoneRequest() =>
        new("1001", "2001", "Apple iPhone", "Telefono de prueba", 800m, 10,
            "https://example.test/iphone.png");

    private Task SeedProductsAsync(params Product[] products) => WithDatabaseAsync(async db =>
    {
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    });

    private async Task<ProductPage<T>> GetPageAsync<T>(string path)
    {
        using var response = await Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        return Assert.IsType<ProductPage<T>>(await response.Content.ReadFromJsonAsync<ProductPage<T>>());
    }

    private sealed record ProductPage<T>(T[] Items, int TotalCount, int PageNumber, int PageSize, int TotalPages);
    private sealed record ApiError(string Code, string Message);
}
