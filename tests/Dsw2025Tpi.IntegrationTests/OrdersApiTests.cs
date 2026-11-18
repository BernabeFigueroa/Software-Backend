using System.Net;
using System.Net.Http.Json;
using Dsw2025Tpi.Application.Dtos.Orders;
using Dsw2025Tpi.Data.Identity;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dsw2025Tpi.IntegrationTests;

[Collection("IntegrationTests")]
public sealed class OrdersApiTests(CustomWebApplicationFactory factory)
    : ApiIntegrationTestBase(factory)
{
    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        using var response = await Client.PostAsJsonAsync("/api/orders",
            NewOrder(new OrderItemRequest(Guid.NewGuid(), 1)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await WithDatabaseAsync(async db => Assert.False(await db.Orders.AnyAsync()));
    }

    [Fact]
    public async Task Create_AsCustomer_ReturnsCreatedAndPersistsOrderItemsAndStock()
    {
        var customer = NewCustomer();
        var iphone = NewIphone();
        await SeedAsync(customer, iphone);
        AuthenticateAs(AppRoles.Cliente, customer.Id);
        var request = NewOrder(new OrderItemRequest(iphone.Id, 3));

        using var response = await Client.PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = Assert.IsType<OrderResponse>(await response.Content.ReadFromJsonAsync<OrderResponse>());
        Assert.NotEqual(Guid.Empty, created.Id);
        Assert.NotNull(response.Headers.Location);
        var location = new Uri(Client.BaseAddress!, response.Headers.Location);
        Assert.Equal($"/api/orders/{created.Id}", location.AbsolutePath);
        Assert.Equal(customer.Id, created.CustomerId);
        Assert.Equal(2400m, created.TotalAmount);
        var createdItem = Assert.Single(created.Items);
        Assert.Equal(iphone.Id, createdItem.ProductId);
        Assert.Equal(3, createdItem.Quantity);

        await WithDatabaseAsync(async db =>
        {
            var order = await db.Orders.Include(o => o.Items).SingleAsync();
            var item = Assert.Single(order.Items);
            Assert.Equal(created.Id, order.Id);
            Assert.Equal(customer.Id, order.CustomerId);
            Assert.Equal(request.ShippingAddress, order.ShippingAddress);
            Assert.Equal(request.BillingAddress, order.BillingAddress);
            Assert.Equal(2400m, order.TotalAmount);
            Assert.Equal(order.Id, item.OrderId);
            Assert.Equal(iphone.Id, item.ProductId);
            Assert.Equal(3, item.Quantity);
            Assert.Equal(800m, item.UnitPrice);
            Assert.Equal(7, (await db.Products.SingleAsync()).StockQuantity);
        });
    }

    [Fact]
    public async Task Create_WithInsufficientStock_ReturnsBadRequestWithoutPartialChanges()
    {
        var customer = NewCustomer();
        var iphone = NewIphone();
        var airpods = Product.Create("1002", "2002", "Apple AirPods", "", 150m, 2);
        await SeedAsync(customer, iphone, airpods);
        AuthenticateAs(AppRoles.Cliente, customer.Id);
        var request = NewOrder(new OrderItemRequest(iphone.Id, 3), new OrderItemRequest(airpods.Id, 5));

        using var response = await Client.PostAsJsonAsync("/api/orders", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var error = Assert.IsType<ApiError>(await response.Content.ReadFromJsonAsync<ApiError>());
        Assert.Equal("ORDER_INSUFFICIENT_STOCK", error.Code);
        Assert.Equal(400, error.Status);
        Assert.Contains("stock insuficiente", error.Message, StringComparison.OrdinalIgnoreCase);

        await WithDatabaseAsync(async db =>
        {
            Assert.False(await db.Orders.AnyAsync());
            Assert.False(await db.OrderItems.AnyAsync());
            Assert.Equal(10, (await db.Products.SingleAsync(p => p.Id == iphone.Id)).StockQuantity);
            Assert.Equal(2, (await db.Products.SingleAsync(p => p.Id == airpods.Id)).StockQuantity);
        });
    }

    private static Customer NewCustomer() => Customer.Create("cliente@example.test", "Cliente de prueba", null);

    private static Product NewIphone() => Product.Create("1001", "2001", "Apple iPhone", "", 800m, 10);

    private static OrderRequest NewOrder(params OrderItemRequest[] items) =>
        new("Calle de envio 123", "Calle de facturacion 456", null, items);

    private Task SeedAsync(Customer customer, params Product[] products) => WithDatabaseAsync(async db =>
    {
        db.Customers.Add(customer);
        db.Products.AddRange(products);
        await db.SaveChangesAsync();
    });

    private sealed record ApiError(string Code, string Message, int Status);
}
