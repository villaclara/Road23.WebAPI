﻿namespace Road23.WebAPI.Models
{
	public class CandleOrder
	{
		public int CandleId { get; set; }
		public int OrderId { get; set; }
		public CandleItem Candle { get; set; } = null!;
		public Order Order { get; set; } = null!;
	}
}
