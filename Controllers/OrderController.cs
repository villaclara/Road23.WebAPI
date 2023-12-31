﻿using Microsoft.AspNetCore.Mvc;
using Road23.WebAPI.Interfaces;
using Road23.WebAPI.Models;
using Road23.WebAPI.Utility.ExtensionMethods;
using Road23.WebAPI.ViewModels;
using System.Collections.Generic;

namespace Road23.WebAPI.Controllers
{
    [ApiController]
	[Route("api/[controller]")]
	public class OrderController : ControllerBase
	{
		private readonly IOrderRepository _orderRepository;
		private readonly IOrderDetailsRepository _detailsRepository;
		private readonly IReceiverRepository _receiverRepository;
		public OrderController(IOrderRepository orderRepository, IOrderDetailsRepository detailsRepository, IReceiverRepository receiverRepository)
		{
			_orderRepository = orderRepository;
			_detailsRepository = detailsRepository;
			_receiverRepository = receiverRepository;
		}



		[HttpPost]
		public async Task<IActionResult> AddOrderAsync([FromBody] OrderFullVM orderToAdd)
		{
			// orderVM validation from Utility folder as Extension
			if(!orderToAdd.Validate())
			{
				return BadRequest("Validation Error");
			}


			var receiversCount = _receiverRepository.GetReceiversByPhone(orderToAdd.Receiver.PhoneNumber.Trim()).Count;

			// mapping from OrderFullVM Viewmodel to Order model
			var ordr = orderToAdd.CovertFromVM_ToDefaultOrder();
			ordr.ReceiverRepeat = receiversCount + 1;
			
			// adding order - it will add both Order and OrderDetails to db
			var isSuccess = await _orderRepository.CreateOrderAsync(ordr);
			if(!isSuccess)
			{
				ModelState.AddModelError("", "Internal error when creating order.");
				return StatusCode(500, ModelState);
			}

			return Ok("Order added");

		}

		[HttpDelete("oid={orderId:int}")]
		[ProducesResponseType(404)]
		[ProducesResponseType(200)]
		public async Task<IActionResult> DeleteOrderAsync(int orderId)
		{
			var order = _orderRepository.GetOrderById(orderId);
			if (order is null)
				return NotFound($"Order {orderId} - Not found.");


			var isSuccess = await _orderRepository.DeleteOrderAsync(order);
			if (!isSuccess)
			{
				ModelState.AddModelError("", "Internal error when deleting order.");
				return StatusCode(500, ModelState);
			}

			var receiver = _receiverRepository.GetReceiverByOrderId(orderId);
			if (receiver is not null)
			{
				await _receiverRepository.DeleteReceiverAsync(receiver);
			}
			// It seems that previous method also deletes the all OrderDetails linked with order
			// But just to be sure I will leave it here
			var details = _detailsRepository.GetOrderDetailsByOrderId(orderId);
			foreach (var d in details)
			{
				await _detailsRepository.RemoveOrderDetailsFromOrderAsync(orderId, d);
			}

			//var receiver = order.Receiver;
			//await _receiverRepository.DeleteReceiverAsync(receiver);

			return Ok($"Order deleted - {orderId}");
		}


		[HttpPut("oid={orderId:int}")]
		[ProducesResponseType(200)]
		[ProducesResponseType(404)]
		[ProducesResponseType(400)]
		public async Task<IActionResult> EditOrderAsync(int orderId, [FromBody] OrderFullVM orderToUpdate)
		{
			if (orderToUpdate is null || orderToUpdate.Id != orderId)
				return StatusCode(400, ModelState);

			if (!_orderRepository.OrderExistsById(orderId))
				return StatusCode(404, $"Order {orderId} - Not found.");

			if (!ModelState.IsValid)
				return StatusCode(400, ModelState);


			var r = _receiverRepository.GetReceiverByOrderId(orderId);

			// deleting all OrderDetails related to order by id
			// because otherwise It adds new details instead of changing current 
			await _detailsRepository.RemoveAllOrderDetailsByOrderId(orderId);

			// deleting receiver to order id
			// because otherwise IT adds new received instead of changing current 
			await _receiverRepository.DeleteReceiverAsync(_receiverRepository.GetReceiverByOrderId(orderId)!);

			// new order from OrderVM
			var ordr = new Order
			{
				Id = orderId,
				OrderDate = orderToUpdate.OrderDate,
				Promocode = orderToUpdate.Promocode,
				TotalSum = orderToUpdate.TotalSum,
				CustomerId = orderToUpdate.CustomerId,
				Comments = orderToUpdate.Comments,
				IsPaid = orderToUpdate.IsPaid,
				PaymentType = orderToUpdate.PaymentType switch
				{
					0 => PaymentType.Cash,
					1 => PaymentType.Card,
					2 => PaymentType.ZD,
					_ => PaymentType.Cash
				},
				Receiver = orderToUpdate.Receiver.ConvertFromReceiverVM_ToReceiverModel(),
				OrderDetails = new List<OrderDetails>()
			};
			foreach (var item in orderToUpdate.OrderDetails)
			{
				ordr.OrderDetails.Add(new OrderDetails
				{
					OrderId = orderId,
					CandleId = item.CandleId,
					CandleQuantity = item.CandleQuantity,
				});
			}

			// Updating Order also adds all OrderDetails to DB
			var isSuccess = await _orderRepository.UpdateOrderAsync(ordr);
			if (!isSuccess)
			{
				ModelState.AddModelError("", "Internal error when updating order.");
				return StatusCode(500, ModelState);
			}

			return Ok(orderToUpdate);
		}


		[HttpGet]
		[ProducesResponseType(200)]
		[ProducesResponseType(404)]
		public IActionResult GetOrders()
		{
			// getting orders
			var orders = _orderRepository.GetOrders();
			if (!orders.Any())
				return StatusCode(404, "Orders Not Found");
			
			var orderVMs = MakeOrderVMsCollection(orders);

			if (!ModelState.IsValid)
				return StatusCode(400, ModelState);
			
			return Ok(orderVMs);
		}


		[HttpGet("oid={orderId:int}")]
		[ProducesResponseType(200)]
		[ProducesResponseType(404)]
		public IActionResult GetOrderById(int orderId)
		{
			var order = _orderRepository.GetOrderById(orderId);
			if (order is null || order is default(Order))
				return NotFound($"Order {orderId} - Not found.");

			var orderVM = order.ConvertFromDefaultOrder_ToFullVM();
			
			return Ok(orderVM);

		}


		[HttpGet("cid={customerId:int}")]
		[ProducesResponseType(200)]
		[ProducesResponseType(404)]
		public IActionResult GetOrdersByCustomerId(int customerId)
		{
			var orders = _orderRepository.GetOrdersByCustomerId(customerId);
			if (!orders.Any())
				return StatusCode(404, $"Orders by Customer {customerId} - Not found.");

			var orderVMs = MakeOrderVMsCollection(orders);

			return Ok(orderVMs);

		}

		[HttpGet("phone={phoneNumber}")]
		[ProducesResponseType(200)]
		[ProducesResponseType(404)]
		public IActionResult GetOrdersByPhoneNumber(string phoneNumber)
		{
			var orders = _orderRepository.GetOrdersByPhoneNumber(phoneNumber);
			if (!orders.Any())
				return StatusCode(404, $"Ordes by phone number - {phoneNumber} - not found.");

			var orderVMs = MakeOrderVMsCollection(orders);

			return Ok(orderVMs);
		}

		[HttpGet("date={date}")]
		[ProducesResponseType(200)]
		[ProducesResponseType(404)]
		public IActionResult GetOrdersByDate(DateOnly date)
		{
			var orders = _orderRepository.GetOrdersByDate(date);
			if (!orders.Any())
				return StatusCode(404, ModelState);

			var orderVMs = MakeOrderVMsCollection(orders);

			return Ok(orderVMs);

		}


		[HttpGet("minsum={minSum:int}")]
		[ProducesResponseType(200)]
		[ProducesResponseType(404)]
		public IActionResult GetOrdersByMinSum(int minSum)
		{
			var orders = _orderRepository.GetOrdersByMinimalSum(minSum);
			if (!orders.Any())
				return StatusCode(404);

			var orderVMs = MakeOrderVMsCollection(orders);
			return Ok(orderVMs);
		}


		[HttpGet("maxsum={maxSum:int}")]
		[ProducesResponseType(200)]
		[ProducesResponseType(404)]
		public IActionResult GetOrdersByMaxSum(int maxSum)
		{
			var orders = _orderRepository.GetOrdersByMaximalSum(maxSum);
			if (!orders.Any())
				return NotFound();

			var orderVMs = MakeOrderVMsCollection(orders);
			return Ok(orderVMs);
		}


		/// <summary>
		/// Getting OrderDetails for each Order in <paramref name="orders"/> and adds into Collection of OrderFullVM.
		/// </summary>
		/// <param name="orders">Orders returned by some criteria</param>
		/// <returns>ICollection of <see cref="OrderFullVM"/> objects</returns>
		private static ICollection<OrderFullVM> MakeOrderVMsCollection(ICollection<Order> orders)
		{
			IList<OrderFullVM> orderVMs = new List<OrderFullVM>();
			foreach (var o in orders)
			{
				orderVMs.Add(o.ConvertFromDefaultOrder_ToFullVM());
			}
			return orderVMs;
		}
	}
}
