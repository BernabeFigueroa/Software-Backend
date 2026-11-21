using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dsw2025Tpi.Application.Dtos.Orders;
using Dsw2025Tpi.Application.Dtos.Products;
using Dsw2025Tpi.Application.Dtos.Users;
using Dsw2025Tpi.Data.Identity;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dsw2025Tpi.IntegrationTests;

[Collection("IntegrationTests")]
public sealed class PurchaseFlowTests(CustomWebApplicationFactory factory)
    : ApiIntegrationTestBase(factory)
{
    [Fact]
    public async Task PurchaseIphone_FromLoginToOrderConfirmation_PersistsOrderAndUpdatesStock()
    {
        const string username = "cliente-e2e";
        const string password = "SoloParaPruebas!123";
        const int initialStock = 10;
        const int quantity = 3;
        var customer = Customer.Create("cliente-e2e@example.test", "Cliente E2E", null);
        var iphone = Product.Create("1001", "2001", "Apple iPhone", "", 800m, initialStock);
        await SeedPurchaseAsync(customer, iphone, username, password);

        using var login = await Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(username, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = Assert.IsType<LoginPayload>(await login.Content.ReadFromJsonAsync<LoginPayload>());
        Assert.False(string.IsNullOrWhiteSpace(session.Token));
        Assert.Equal(AppRoles.Cliente, session.Role);
        Assert.Equal(customer.Id, session.CustomerId);
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);

        using var productResponse = await Client.GetAsync($"/api/products/{iphone.Id}");
        Assert.Equal(HttpStatusCode.OK, productResponse.StatusCode);
        var product = Assert.IsType<ProductResponse>(await productResponse.Content.ReadFromJsonAsync<ProductResponse>());
        Assert.Equal(iphone.Id, product.Id);
        Assert.Equal(initialStock, product.StockQuantity);

        var request = new OrderRequest("Calle de envio 123", "Calle de facturacion 456", null,
            new[] { new OrderItemRequest(product.Id, quantity) });
        using var purchase = await Client.PostAsJsonAsync("/api/orders", request);
        Assert.Equal(HttpStatusCode.Created, purchase.StatusCode);
        var created = Assert.IsType<OrderResponse>(await purchase.Content.ReadFromJsonAsync<OrderResponse>());
        Assert.NotNull(purchase.Headers.Location);

        using var confirmation = await Client.GetAsync(purchase.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        var order = Assert.IsType<OrderResponse>(await confirmation.Content.ReadFromJsonAsync<OrderResponse>());
        Assert.Equal(created.Id, order.Id);
        Assert.Equal(customer.Id, order.CustomerId);
        Assert.Equal(2400m, order.TotalAmount);
        var item = Assert.Single(order.Items);
        Assert.Equal(iphone.Id, item.ProductId);
        Assert.Equal(quantity, item.Quantity);

        using var updatedResponse = await Client.GetAsync($"/api/products/{iphone.Id}");
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = Assert.IsType<ProductResponse>(await updatedResponse.Content.ReadFromJsonAsync<ProductResponse>());
        Assert.Equal(initialStock - quantity, updated.StockQuantity);
        await WithDatabaseAsync(async db =>
        {
            var savedOrder = await db.Orders.Include(o => o.Items).SingleAsync();
            Assert.Equal(order.Id, savedOrder.Id);
            Assert.Equal(customer.Id, savedOrder.CustomerId);
            var savedItem = Assert.Single(savedOrder.Items);
            Assert.Equal(iphone.Id, savedItem.ProductId);
            Assert.Equal(quantity, savedItem.Quantity);
            Assert.Equal(initialStock - quantity, (await db.Products.SingleAsync()).StockQuantity);
        });
    }

    private async Task SeedPurchaseAsync(Customer customer, Product product, string username, string password)
    {
        await WithDatabaseAsync(async db =>
        {
            db.Customers.Add(customer);
            db.Products.Add(product);
            await db.SaveChangesAsync();
        });

        await using var scope = Factory.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser { UserName = username, Email = customer.Email, CustomerId = customer.Id };

        AssertSucceeded(await roles.CreateAsync(new IdentityRole(AppRoles.Cliente)));
        AssertSucceeded(await users.CreateAsync(user, password));
        AssertSucceeded(await users.AddToRoleAsync(user, AppRoles.Cliente));
    }

    private static void AssertSucceeded(IdentityResult result) =>
        Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(error => error.Description)));

    private sealed record LoginPayload(string Token, string Role, Guid CustomerId);
}
