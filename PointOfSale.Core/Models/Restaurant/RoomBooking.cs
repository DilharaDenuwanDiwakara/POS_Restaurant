using System;
using PointOfSale.Core.Enums;

namespace PointOfSale.Core.Models.Restaurant
{
    public class RoomBooking
    {
        public int Id { get; set; }
        public int RoomId { get; set; }
        public int MealPeriodId { get; set; }
        public string RoomName { get; set; }
        public string MealPeriodName { get; set; }
        public DateTime From { get; set; }
        public DateTime To { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhoneNumber { get; set; }
        public int NumberOfPax { get; set; }
        public BookingStatus Status { get; set; } = BookingStatus.Pending;
        public string Remarks { get; set; }
        public int? CustomerId { get; set; }
        public decimal AdvancePayment { get; set; }
        public int CreatedBy { get; set; }
    }
}
