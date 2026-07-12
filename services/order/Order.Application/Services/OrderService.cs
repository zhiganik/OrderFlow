using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MassTransit;
using Microsoft.Extensions.Logging;
using Order.Application.Domain;
using Order.Application.Dtos;
using Order.Application.Interfaces;
using OrderFlow.Contracts;
using OrderFlow.Shared.Common;

namespace Order.Application.Services;

public class OrderService(
    IOrderRepository orderRepository,
    IIdempotencyKeyRepository idempotencyKeyRepository,
    IUnitOfWork unitOfWork,
    IPublishEndpoint publishEndpoint,
    ILogger<OrderService> logger) : IOrderService
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

        await publishEndpoint.Publish(
            new OrderCreatedEvent(
                order.Id,
                order.CustomerId,
                order.Items.Select(i => new OrderCreatedItem(i.ProductName, i.Quantity)).ToList(),
                order.CreatedAt),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateOrderResult { Outcome = CreateOrderOutcome.Created, StatusCode = 201, Order = response };
    }

    public async Task<PagedResult<OrderResponse>> GetOrdersByCustomerAsync(
        Guid customerId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (orders, totalCount) = await orderRepository.GetPagedByCustomerIdAsync(customerId, page, pageSize, cancellationToken);

        return new PagedResult<OrderResponse>
        {
            Items = orders.Select(ToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<PagedResult<OrderResponse>> GetAllOrdersAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (orders, totalCount) = await orderRepository.GetAllPagedAsync(page, pageSize, cancellationToken);

        return new PagedResult<OrderResponse>
        {
            Items = orders.Select(ToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<OrderResponse?> GetOrderByIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await orderRepository.GetByIdAsync(orderId, cancellationToken);
        return order is null ? null : ToResponse(order);
    }

    public async Task MarkReservedAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await orderRepository.FindByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Received InventoryReservedEvent for unknown order {OrderId}", orderId);
            return;
        }

        order.MarkReserved();
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkRejectedAsync(Guid orderId, string reason, CancellationToken cancellationToken = default)
    {
        var order = await orderRepository.FindByIdAsync(orderId, cancellationToken);
        if (order is null)
        {
            logger.LogWarning("Received InventoryRejectedEvent for unknown order {OrderId}", orderId);
            return;
        }

        order.MarkRejected(reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static OrderResponse ToResponse(OrderEntity order) => new()
    {
        Id = order.Id,
        CustomerId = order.CustomerId,
        Status = order.Status.ToString(),
        RejectionReason = order.RejectionReason,
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
