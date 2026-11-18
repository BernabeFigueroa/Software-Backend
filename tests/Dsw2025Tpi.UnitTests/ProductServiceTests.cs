using System.Linq.Expressions;
using AutoMapper;
using Dsw2025Tpi.Application.Dtos.Products;
using Dsw2025Tpi.Application.Interfaces;
using Dsw2025Tpi.Application.Mappings;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Exceptions.Common;
using Dsw2025Tpi.Domain.Exceptions.ProductExceptions;
using Dsw2025Tpi.Domain.Interfaces;
using Moq;
using Xunit;

namespace Dsw2025Tpi.UnitTests;

public class ProductServiceTests
{
    private readonly Mock<IRepository<Product>> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ProductService _service;
    private static readonly IMapper Mapper = new MapperConfiguration(
        config => config.AddProfile<MappingProfiles>()).CreateMapper();

    public ProductServiceTests()
    {
        _service = new ProductService(_repository.Object, _unitOfWork.Object, Mapper);
    }

    [Fact]
    public async Task AddProduct_WithValidData_ReturnsProductAndCallsRepositoryAdd()
    {
        // Arrange
        var request = ValidRequest();
        _repository.Setup(repo => repo.First(
                It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
            .ReturnsAsync((Product?)null);
        Product? addedProduct = null;
        _repository.Setup(repo => repo.Add(It.IsAny<Product>()))
            .ReturnsAsync((Product product) => addedProduct = product);
        _unitOfWork.Setup(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // Act
        var result = await _service.CreateAsync(request);

        // Assert
        _repository.Verify(repo => repo.Add(It.IsAny<Product>()), Times.Once);
        _unitOfWork.Verify(work => work.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.NotNull(addedProduct);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(new ProductResponse(addedProduct.Id, request.Sku, request.InternalCode,
            request.Name, request.Description, request.CurrentUnitPrice, request.StockQuantity,
            request.ImageUrl, true), result);
    }

    [Fact]
    public async Task AddProduct_WithDuplicateSku_ThrowsProductSkuAlreadyExistsException()
    {
        // Arrange
        var request = ValidRequest();
        var existing = Product.Create(request.Sku, "2002", "Apple AirPods", "Auriculares inalambricos", 150m, 1);
        _repository.Setup(repo => repo.First(
                It.IsAny<Expression<Func<Product, bool>>>(), It.IsAny<string[]>()))
            .ReturnsAsync((Expression<Func<Product, bool>> predicate, string[] _) =>
                predicate.Compile()(existing) ? existing : null);

        // Act
        var exception = await Assert.ThrowsAsync<ProductSkuAlreadyExistsException>(
            () => _service.CreateAsync(request));

        // Assert
        Assert.Equal(request.Sku, exception.Sku);
        Assert.Equal(existing.Id, exception.ExistingProductId);
        Assert.Contains(request.Sku, exception.Message);
        _repository.Verify(repo => repo.Add(It.IsAny<Product>()), Times.Never);
        _unitOfWork.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, 100, typeof(InvalidProductStockException), "PRODUCT_INVALID_STOCK", "stock")]
    [InlineData(-1, 100, typeof(InvalidProductStockException), "PRODUCT_INVALID_STOCK", "stock")]
    [InlineData(5, -1, typeof(InvalidProductPriceException), "PRODUCT_INVALID_PRICE", "precio")]
    public async Task AddProduct_WithInvalidStockOrPrice_RejectsBeforeAccessingRepository(
        int stock, int price, Type exceptionType, string errorCode, string messagePart)
    {
        // Arrange
        var request = ValidRequest() with { StockQuantity = stock, CurrentUnitPrice = price };

        // Act
        var exception = await Record.ExceptionAsync(() => _service.CreateAsync(request));

        // Assert
        Assert.IsType(exceptionType, exception);
        var domainException = Assert.IsAssignableFrom<ExceptionBase>(exception);
        Assert.Equal(errorCode, domainException.Code);
        Assert.Contains(messagePart, domainException.Message, StringComparison.OrdinalIgnoreCase);
        _repository.VerifyNoOtherCalls();
        _unitOfWork.VerifyNoOtherCalls();
    }

    private static ProductRequest ValidRequest() =>
        new("1001", "2001", "Apple iPhone", "Telefono de Apple", 100m, 5, null);
}
