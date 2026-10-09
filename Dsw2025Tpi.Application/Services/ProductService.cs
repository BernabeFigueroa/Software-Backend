using AutoMapper;
using Dsw2025Tpi.Application.Dtos.Products;
using Dsw2025Tpi.Application.Interfaces;
using Dsw2025Tpi.Application.Pagination;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Exceptions.ProductExceptions;
using Dsw2025Tpi.Domain.Interfaces;
using System.Linq;

namespace Dsw2025Tpi.Application.Services;

public sealed class ProductService : IProductService
{
    private readonly IRepository<Product> _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public ProductService(IRepository<Product> productRepository, IUnitOfWork unitOfWork, IMapper mapper)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    // 1) Crear producto  → POST /api/products
    public async Task<ProductResponse> CreateAsync(ProductRequest request)
    {
        var skuLower = request.Sku.Trim().ToLowerInvariant();
        var codeLower = request.InternalCode.Trim().ToLowerInvariant();

        // Buscar por SKU o InternalCode (activos o inactivos)
        var existing = await _productRepository.First(p =>
            p.Sku.ToLower() == skuLower ||
            p.InternalCode.ToLower() == codeLower);

        if (existing is not null)
        {
            // Determinar si el conflicto es por SKU o InternalCode
            var isSkuConflict = existing.Sku.Equals(request.Sku, StringComparison.OrdinalIgnoreCase);

            if (isSkuConflict)
            {
                // Conflicto por SKU
                if (!existing.IsActive)
                {
                    throw new ProductSkuAlreadyExistsDisabledException(request.Sku, existing.Id);
                }
                throw new ProductSkuAlreadyExistsException(request.Sku, existing.Id);
            }
            else
            {
                // Conflicto por InternalCode
                if (!existing.IsActive)
                {
                    throw new ProductInternalCodeAlreadyExistsDisabledException(request.InternalCode, existing.Id);
                }
                throw new ProductInternalCodeAlreadyExistsException(request.InternalCode, existing.Id);
            }
        }

        // Si no existe, creamos el producto normalmente
        // Las validaciones de precio, stock, SKU, etc. se lanzan automáticamente desde Product.Create()
        var product = Product.Create(
            request.Sku,
            request.InternalCode,
            request.Name,
            request.Description ?? string.Empty,
            request.CurrentUnitPrice,
            request.StockQuantity,
            request.ImageUrl);

        await _productRepository.Add(product);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<ProductResponse>(product);
    }


    // 2) Obtener todos los productos activos con paginación → GET /api/products
    public async Task<PagedResult<ProductResponse>> GetAllAsync(int pageNumber = 1, int pageSize = 10, string? search = null)
    {
        var query = _productRepository.GetAllQueryable();

        // Filtrar solo productos activos
        query = query.Where(p => p.IsActive);

        // Filtro de búsqueda por SKU y Nombre
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();

            query = query.Where(p =>
                p.Sku.Contains(term) ||
                p.Name.Contains(term));
        }

        // Normalizar paginación
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0) pageSize = 10;

        const int maxPageSize = 100;
        if (pageSize > maxPageSize)
            pageSize = maxPageSize;

        // Total antes de paginar
        var totalCount = query.Count();

        if (totalCount == 0)
        {
            throw new NoProductsAvailableException();
        }

        // Paginación + proyección a DTO
        var items = query
            .OrderBy(p => p.Sku) // criterio estable
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dtoItems = items
            .Select(p => _mapper.Map<ProductResponse>(p))
            .ToList();

        return PagedResult<ProductResponse>.Create(
            dtoItems,
            totalCount,
            pageNumber,
            pageSize);
    }

    // 3) Obtener producto por Id → GET /api/products/{id}
    //    El controller mapea null → 404 NotFound
    public async Task<ProductResponse?> GetByIdAsync(Guid productId)
    {
        var product = await _productRepository.GetById(productId);

        if (product is null)
            throw new ProductNotFoundException(productId);
        if (!product.IsActive)
            throw new ProductInactiveException();

        return _mapper.Map<ProductResponse>(product);
    }

    // 4) Actualizar producto → PUT /api/products/{id}
    public async Task<ProductResponse?> UpdateAsync(Guid productId, ProductUpdateRequest request)
    {
        var product = await _productRepository.GetById(productId);

        if (product is null)
        {
            throw new ProductNotFoundException(productId);
        }

        var skuLower = request.Sku.Trim().ToLowerInvariant();
        var codeLower = request.InternalCode.Trim().ToLowerInvariant();

        // Buscar si existe otro producto (diferente al actual) con el mismo SKU o InternalCode
        var existing = await _productRepository.First(p =>
            p.Id != productId &&
            (p.Sku.ToLower() == skuLower || p.InternalCode.ToLower() == codeLower));

        if (existing is not null)
        {
            // Determinar si el conflicto es por SKU o InternalCode
            var isSkuConflict = existing.Sku.Equals(request.Sku, StringComparison.OrdinalIgnoreCase);

            if (isSkuConflict)
            {
                // Conflicto por SKU
                if (!existing.IsActive)
                {
                    throw new ProductSkuAlreadyExistsDisabledException(request.Sku, existing.Id);
                }
                throw new ProductSkuAlreadyExistsException(request.Sku, existing.Id);
            }
            else
            {
                // Conflicto por InternalCode
                if (!existing.IsActive)
                {
                    throw new ProductInternalCodeAlreadyExistsDisabledException(request.InternalCode, existing.Id);
                }
                throw new ProductInternalCodeAlreadyExistsException(request.InternalCode, existing.Id);
            }
        }

        // Las validaciones de precio, stock, SKU, etc. se lanzan automáticamente desde UpdateDetails()
        product.UpdateDetails(
            request.Sku,
            request.InternalCode,
            request.Name,
            request.Description ?? string.Empty,
            request.CurrentUnitPrice,
            request.StockQuantity,
            request.ImageUrl);

        await _productRepository.Update(product);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<ProductResponse>(product);
    }

    // 5) Inhabilitar producto → PATCH /api/products/{id}
    public async Task DisableAsync(Guid productId)
    {
        var product = await _productRepository.GetById(productId);

        if (product is null)
        {
            throw new ProductNotFoundException(productId);
        }

        product.Deactivate();

        await _productRepository.Update(product);
        await _unitOfWork.SaveChangesAsync();
    }

    // 6) Habilitar producto → PATCH /api/products/{id}/enable
    public async Task EnableAsync(Guid productId)
    {
        var product = await _productRepository.GetById(productId);

        if (product is null)
        {
            throw new ProductNotFoundException(productId);
        }

        product.Activate();

        await _productRepository.Update(product);
        await _unitOfWork.SaveChangesAsync();
    }

    // 7) Paginación de productos → GET /api/products/paged?...
    public Task<PagedResult<ProductListItemDto>> GetPagedAsync(FilterProductRequest filter, CancellationToken cancellationToken = default)
    {
        // i) Query base
        var query = _productRepository.GetAllQueryable();

        // ii) Filtro por estado (activo / inactivo)
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var status = filter.Status.Trim().ToLowerInvariant();

            query = status switch
            {
                "enabled" or "active" or "true" => query.Where(p => p.IsActive),
                "disabled" or "inactive" or "false" => query.Where(p => !p.IsActive),
                _ => query // valor inválido: no filtramos
            };
        }

        // iii) Filtro de búsqueda por SKU, InternalCode y Nombre.
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();

            query = query.Where(p =>
                p.Sku.Contains(term) ||
                p.InternalCode.Contains(term) ||
                p.Name.Contains(term));
        }

        // iv) Normalizar paginación
        int pageNumber = (filter.PageNumber.HasValue && filter.PageNumber > 0) ? filter.PageNumber.Value : 1;
        int pageSize = (filter.PageSize.HasValue && filter.PageSize > 0) ? filter.PageSize.Value : 10;

        const int maxPageSize = 100;
        if (pageSize > maxPageSize)
            pageSize = maxPageSize;

        // v) Total antes de paginar
        var totalCount = query.Count();

        if (totalCount == 0)
        {
            throw new NoProductsAvailableException();
        }

        // vi) Paginación + proyección a DTO liviano
        var items = query
            .OrderBy(p => p.Sku) // criterio estable
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItemDto(
                p.Id,
                p.Sku,
                p.Name,
                p.CurrentUnitPrice,
                p.ImageUrl,
                p.IsActive))
            .ToList();

        var result = PagedResult<ProductListItemDto>.Create(
            items,
            totalCount,
            pageNumber,
            pageSize);

        return Task.FromResult(result);
    }
}


