using System;
using System.Collections;
using System.Globalization;
using PointOfSale.Core.DTOs;

namespace PointOfSale.UI.ViewModels.Sales
{
    public enum ProductSortField
    {
        Code,
        Name
    }

    internal sealed class MenuProductComparer : IComparer
    {
        private readonly ProductSortField _field;
        private readonly StringComparer _textComparer = StringComparer.CurrentCultureIgnoreCase;

        public MenuProductComparer(ProductSortField field)
        {
            _field = field;
        }

        public int Compare(object x, object y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            var left = (MenuVariantDto)x;
            var right = (MenuVariantDto)y;
            if (_field == ProductSortField.Name)
            {
                var byName = _textComparer.Compare(left.DisplayName, right.DisplayName);
                if (byName != 0) return byName;
            }

            // Preserve numeric-first code ordering (2 before 10) used by the repository.
            int leftNumber, rightNumber;
            var leftKey = int.TryParse(left.ItemCode, NumberStyles.Integer, CultureInfo.CurrentCulture, out leftNumber)
                ? leftNumber : int.MaxValue;
            var rightKey = int.TryParse(right.ItemCode, NumberStyles.Integer, CultureInfo.CurrentCulture, out rightNumber)
                ? rightNumber : int.MaxValue;
            var byNumber = leftKey.CompareTo(rightKey);
            if (byNumber != 0) return byNumber;
            var byCode = _textComparer.Compare(left.ItemCode, right.ItemCode);
            return byCode != 0 ? byCode : left.VariantId.CompareTo(right.VariantId);
        }
    }
}
