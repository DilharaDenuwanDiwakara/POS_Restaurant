using System.Collections.Generic;
using System.Threading.Tasks;
using PointOfSale.Core.Enums;
using PointOfSale.Core.Models.Restaurant;

namespace PointOfSale.Core.Interfaces.Repositories.Restaurant
{
    public interface IRoomBookingRepository
    {
        Task<int> CreateRoomAsync(Room room);
        Task UpdateRoomAsync(Room room);
        Task<IEnumerable<Room>> GetAllRoomsAsync();

        Task<int> CreateBookingAsync(RoomBooking booking);
        Task UpdateBookingAsync(RoomBooking booking);
        Task<IEnumerable<RoomBooking>> GetAllBookingsAsync();
        Task UpdateBookingStatusAsync(int bookingId, BookingStatus newStatus);
    }
}
