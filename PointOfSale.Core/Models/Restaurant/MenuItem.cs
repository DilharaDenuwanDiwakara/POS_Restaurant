using System.Collections.Generic;

namespace PointOfSale.Core.Models.Restaurant
{
    public class MenuItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int CategoryId { get; set; }
        public int StationId { get; set; } // 1=Kitchen, 2=Bar, etc.
        public string ImageUrl { get; set; }
        public bool IsActive { get; set; }
        public bool IsAvailable { get; set; } // Stock status

        public List<Variant> Variants { get; set; } = new List<Variant>();
    }
}
