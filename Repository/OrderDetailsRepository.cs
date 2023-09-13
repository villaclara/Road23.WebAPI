﻿using Road23.WebAPI.Database;
using Road23.WebAPI.Interfaces;
using Road23.WebAPI.Models;

namespace Road23.WebAPI.Repository
{
	public class OrderDetailsRepository : IOrderDetailsRepository
	{
		private readonly ApplicationContext _context;
		public OrderDetailsRepository(ApplicationContext context)
		{
			_context = context;
		}

		public async Task<OrderDetails> AddOrderDetailsToOrderAsync(int orderId, OrderDetails orderDetails)
		{
			_context.OrderDetails.Add(orderDetails);
			await _context.SaveChangesAsync();
			return orderDetails;

		}

		public ICollection<OrderDetails> GetOrderDetailsByOrderId(int orderId) =>
			_context.OrderDetails.Where(o =>  o.OrderId == orderId).ToList();

		public ICollection<OrderDetails>? RemoveOrderDetailsFromOrderAsync(int orderId, OrderDetails orderDetails)
		{
			throw new NotImplementedException();
		}

		public OrderDetails UpdateOrderDetailsInOrderAsync(int orderId, OrderDetails orderDetails)
		{
			throw new NotImplementedException();
		}
	}
}