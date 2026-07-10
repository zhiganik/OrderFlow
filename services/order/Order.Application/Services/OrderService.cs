using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Order.Application.Domain;
using Order.Application.Dtos;
using Order.Application.Interfaces;

namespace Order.Application.Services;

public class OrderService(
    IOrderRepository orderRepository,
    IIdempotencyKeyRepository idempotencyKeyRepository,
    IUnitOfWork unitOfWork) : IOrderService
{
    public async Task<CreateOrderResult> CreateOrderAsync(
        CreateOrderRequest request,
        Guid customerId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var requestHash = HashRequest(customerId, request);

        var existing = await idempotencyKeyRepository.FindAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.RequestHash != requestHash)
            {
                return new CreateOrderResult { Outcome = CreateOrderOutcome.Conflict, StatusCode = 409 };
            }

            return new CreateOrderResult
            {
                Outcome = CreateOrderOutcome.ReplayedFromCache,
                StatusCode = existing.ResponseStatusCode,
                Order = JsonSerializer.Deserialize<OrderResponse>(existing.ResponseBody),
            };
        }

        var order = OrderEntity.Create(customerId, request.Items.Select(i => (i.ProductName, i.Quantity)).ToList());
        var response = ToResponse(order);

        orderRepository.Add(order);
        idempotencyKeyRepository.Add(new IdempotencyKey
        {
            Key = idempotencyKey,
            RequestHash = requestHash,
            ResponseStatusCode = 201,
            ResponseBody = JsonSerializer.Serialize(response),
            CreatedAt = order.CreatedAt,
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateOrderResult { Outcome = CreateOrderOutcome.Created, StatusCode = 201, Order = response };
    }

    public async Task<List<OrderResponse>> GetOrdersByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        var orders = await orderRepository.GetByCustomerIdAsync(customerId, cancellationToken);
        return orders.Select(ToResponse).ToList();
    }

    public async Task<List<OrderResponse>> GetAllOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await orderRepository.GetAllAsync(cancellationToken);
        return orders.Select(ToResponse).ToList();
    }

    public async Task<OrderResponse?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await orderRepository.GetByIdAsync(orderId, cancellationToken);
        return order is null ? null : ToResponse(order);
    }

    private static OrderResponse ToResponse(OrderEntity order) => new()
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        Status = order.Status.ToString(),
        CreatedAt = order.CreatedAt,
        UpdatedAt = order.UpdatedAt,
        Items = order.Items.Select(i => new OrderItemResponse
        {
            Id = i.Id,
            ProductName = i.ProductName,
            Quantity = i.Quantity,
        }).ToList(),
    };

    private static string HashRequest(Guid customerId, CreateOrderRequest request)
    {
        var normalized = JsonSerializer.Serialize(new
        {
            CustomerId = customerId,
            Items = request.Items
                .Select(i => new { i.ProductName, i.Quantity })
                .OrderBy(i => i.ProductName, StringComparer.Ordinal)
                .ThenBy(i => i.Quantity),
        });

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
