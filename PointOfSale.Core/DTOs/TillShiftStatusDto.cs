namespace PointOfSale.Core.DTOs
{
    public class TillShiftStatusDto
    {
        public int TillId { get; set; }
        public string RegisterName { get; set; }
        public int? ShiftId { get; set; }
        public bool RequiresFloat { get; set; }
    }
}
