using AutoMapper;
using Dsw2025Tpi.Application.Dtos.Orders;
using Dsw2025Tpi.Application.Interfaces;
using Dsw2025Tpi.Application.Pagination;
using Dsw2025Tpi.Domain.Entities;
using Dsw2025Tpi.Domain.Enums;
using Dsw2025Tpi.Domain.Exceptions.Common;
using Dsw2025Tpi.Domain.Exceptions.CustomerExceptions;
using Dsw2025Tpi.Domain.Exceptions.OrderExceptions;
using Dsw2025Tpi.Domain.Exceptions.ProductExceptions;
using Dsw2025Tpi.Domain.Interfaces;

namespace Dsw2025Tpi.Application.Services;

public sealed class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IRepository<Product> _productRepository;
    private readonly IRepository<Customer> _customerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMapper _mapper;

    public OrderService(
        IRepository<Order> orderRepository,
        IRepository<Product> productRepository,
        IRepository<Customer> customerRepository,
        IUnitOfWork unitOfWork,
        IMapper mapper)
    {
        _orderRepository = orderRepository;
        _productRepository = productRepository;
        _customerRepository = customerRepository;
        _unitOfWork = unitOfWork;
        _mapper = mapper;
    }

    // ===========================================================
    // 1) CREAR ORDEN
    // ===========================================================
    // Todo el proceso (lectura y validación de stock, descuento de stock e inserción de la orden)
    // se ejecuta dentro de una única transacción. Los repositorios solo registran los cambios y
    // el Unit of Work los envía con un único SaveChanges + Commit. Si algo falla, se hace Rollback.
    public async Task<OrderResponse> CreateOrderAsync(Guid customerId, OrderRequest request)
    {
        if (request.OrderItems is null || !request.OrderItems.Any())
            throw new OrderWithoutItemsException();

        return await _unitOfWork.ExecuteInTransactionAsync(() => CreateOrderCoreAsync(customerId, request));
    }

    private async Task<OrderResponse> CreateOrderCoreAsync(Guid customerId, OrderRequest request)
    {
        // Validar cliente
        var customer = await _customerRepository.GetById(customerId);
        if (customer is null)
            throw new InvalidOrderCustomerException();

        if (!customer.IsActive)
            throw new CustomerInactiveException(customerId);

        // Obtener productos involucrados
        var productIds = request.OrderItems
            .Select(i => i.ProductId)
            .Distinct()
            .ToList();

        var products = await _productRepository
            .GetFiltered(p => productIds.Contains(p.Id))
            ?? new List<Product>();

        var productDict = products.ToDictionary(p => p.Id);

        // Validar existencia
        foreach (var pid in productIds)
        {
            if (!productDict.ContainsKey(pid))
                throw new ProductNotFoundException(pid);
        }

        // Validar stock
        foreach (var item in request.OrderItems)
        {
            var prod = productDict[item.ProductId];

            if (!prod.IsActive)
                throw new ProductInactiveException();

            if (!prod.HasSufficientStock(item.Quantity))
            {
                throw new OrderInsufficientStockException(
                    prod.Id,
                    prod.Name,
                    item.Quantity,
                    prod.StockQuantity
                );
            }
        }

        // Crear orden
        var order = Order.Create(
            customerId,
            request.ShippingAddress,
            request.BillingAddress,
            request.Notes
        );

        // Agregar items con snapshot de nombre y precio
        foreach (var itemReq in request.OrderItems)
        {
            var prod = productDict[itemReq.ProductId];

            order.AddItem(
                prod.Id,
                prod.Name,
                itemReq.Quantity,
                prod.CurrentUnitPrice
            );

            // Se registra el descuento de stock (sin persistir todavía)
            prod.DecreaseStock(itemReq.Quantity);
            await _productRepository.Update(prod);
        }

        order.ValidateHasItems();

        // Se registra la orden (sin persistir todavía)
        await _orderRepository.Add(order);

        return _mapper.Map<OrderResponse>(order);
    }

    // ===========================================================
    // 2) OBTENER ORDEN POR ID
    // ===========================================================
    public async Task<OrderResponse> GetOrderByIdAsync(Guid id)
    {
        var order = await _orderRepository.GetById(id, "Items,Customer"); // Include Items y Customer

        if (order is null)
            throw new OrderNotFoundException(id);

        return _mapper.Map<OrderResponse>(order);
    }

    // ===========================================================
    // 3) ACTUALIZAR ESTADO DE ORDEN
    // ===========================================================
    public async Task<OrderResponse> UpdateOrderStatusAsync(Guid id, string newStatus)
    {
        var order = await _orderRepository.GetById(id, "Items,Customer"); // Include Items y Customer
        if (order is null)
            throw new OrderNotFoundException(id);

        if (!Enum.TryParse<OrderStatus>(newStatus, true, out var status))
            throw new InvalidOrderStatusException(newStatus);

        // Validar si el estado ya está establecido
        if (order.Status == status)
            throw new OrderStatusAlreadySetException(status);

        switch (status)
        {
            case OrderStatus.Pending:
                // No se puede volver a Pending desde otro estado
                throw new InvalidOrderStatusTransitionException(order.Status, status);
            case OrderStatus.Processing:
                order.MarkAsProcessing();
                break;
            case OrderStatus.Shipped:
                order.MarkAsShipped();
                break;
            case OrderStatus.Delivered:
                order.MarkAsDelivered();
                break;
            case OrderStatus.Cancelled:
                order.Cancel();
                break;
            default:
                throw new InvalidOrderStatusTransitionException(order.Status, status);
        }

        await _orderRepository.Update(order);
        await _unitOfWork.SaveChangesAsync();

        return _mapper.Map<OrderResponse>(order);
    }

    // ===========================================================
    // 4) PAGINAR ÓRDENES (para Admin o Cliente)
    // ===========================================================
    public async Task<PagedResult<OrderListItemDto>> GetPagedAsync(
        FilterOrder filter,
        CancellationToken cancellationToken = default)
    {
        var query = _orderRepository.GetAllQueryable("Items,Customer"); // Include Items y Customer

        // Filtrar por estado
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            if (!Enum.TryParse<OrderStatus>(filter.Status, true, out var parsedStatus))
                throw new InvalidOrderStatusException(filter.Status);

            query = query.Where(o => o.Status == parsedStatus);
        }

        // Filtrar por Customer ID
        if (filter.CustomerId.HasValue)
        {
            query = query.Where(o => o.CustomerId == filter.CustomerId.Value);
        }

        // Filtrar por nombre de cliente (solo para admin, no cuando ya hay customerId)
        if (!string.IsNullOrWhiteSpace(filter.CustomerName) && !filter.CustomerId.HasValue)
        {
            var customerNameLower = filter.CustomerName.Trim().ToLower();
            query = query.Where(o => o.Customer != null && o.Customer.Name.ToLower().Contains(customerNameLower));
        }

        // Filtro de búsqueda general
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var searchTerm = filter.Search.ToLower();

            // Si ya estamos filtrando por customerId (caso: cliente viendo sus órdenes)
            // El cliente puede buscar sus ordenes por id, dirección de envío, dirección de facturación o notas
            if (filter.CustomerId.HasValue)
            {
                query = query.Where(o =>
                       o.Id.ToString().Contains(searchTerm) 
                    || o.ShippingAddress.ToLower().Contains(searchTerm)
                    || o.BillingAddress.ToLower().Contains(searchTerm)
                    || (o.Notes != null && o.Notes.ToLower().Contains(searchTerm)));
            }
            else
            {
                // Para admin: buscar también en nombre de cliente
                query = query.Where(o =>
                       o.ShippingAddress.ToLower().Contains(searchTerm)
                    || o.Id.ToString().Contains(searchTerm)
                    || o.BillingAddress.ToLower().Contains(searchTerm)
                    || (o.Notes != null && o.Notes.ToLower().Contains(searchTerm))
                    || (o.Customer != null && o.Customer.Name.ToLower().Contains(searchTerm)));
            }
        }

        // Contar total
        var total = query.Count();

        // Normalizar paginación
        var pageNumber = filter.PageNumber.HasValue && filter.PageNumber.Value > 0 ? filter.PageNumber.Value : 1;
        var pageSize = filter.PageSize.HasValue && filter.PageSize.Value > 0 ? filter.PageSize.Value : 10;
        const int maxSize = 100;
        if (pageSize > maxSize) pageSize = maxSize;

        // Aplicar paginación
        var items = query
            .OrderByDescending(o => o.Date)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var dto = items
            .Select(o => _mapper.Map<OrderListItemDto>(o))
            .ToList();

        return PagedResult<OrderListItemDto>.Create(
            dto,
            total,
            pageNumber,
            pageSize
        );
    }
}
