using System.Collections.ObjectModel;

namespace PointOfSale.Core.DTOs
{
    public class CategoryTreeNode
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        // This collection holds the "Children" for the TreeView to expand
        public ObservableCollection<CategoryTreeNode> Children { get; set; } = new ObservableCollection<CategoryTreeNode>();
    }
}
