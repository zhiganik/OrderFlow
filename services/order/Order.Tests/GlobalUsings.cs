// "Order" alone is ambiguous with the "Order" root namespace shared by every
// project in this service (CS0118), so every project aliases the entity here
// instead of repeating the fully qualified name at every use site.
global using OrderEntity = Order.Application.Domain.Order;
