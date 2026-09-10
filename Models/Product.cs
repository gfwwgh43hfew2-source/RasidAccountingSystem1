using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RasidAccountingSystem.Models
{
    public class Product : INotifyPropertyChanged
    {
        #region Private Fields

        private long _id;
        private string _barcode;
        private string _code;
        private string _name = string.Empty;

        // الوحدات الثلاث للنظام الهرمي
        private string _unit1;
        private string _unit2;
        private string _unit3;

        // عوامل التحويل
        private int _unit1Factor;
        private int _unit2Factor;

        // الأسعار حسب الوحدة
        private decimal _price1;
        private decimal _price2;
        private decimal _price3;

        // الكمية المخزنة بالوحدة الأساسية
        private long _quantityInBaseUnit;

        // حد الطلب
        private int _reorderLevelInBaseUnit;
        private string _reorderUnit;

        // أسعار إضافية
        private decimal _costPrice;
        private decimal _wholesalePrice;
        private long _minQuantity;
        private long _maxQuantity;
        private string _category;

        private bool _isActive = true;
        private bool _isSelected = false;
        private DateTime _createdAt = DateTime.Now;
        private DateTime _updatedAt = DateTime.Now;

        #endregion

        #region Properties

        public long Id
        {
            get => _id;
            set { _id = value; OnPropertyChanged(); }
        }

        public string Barcode
        {
            get => _barcode;
            set { _barcode = value; OnPropertyChanged(); }
        }

        public string Code
        {
            get => _code;
            set { _code = value; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Unit1
        {
            get => _unit1;
            set { _unit1 = value; OnPropertyChanged(); }
        }

        public string Unit2
        {
            get => _unit2;
            set { _unit2 = value; OnPropertyChanged(); }
        }

        public string Unit3
        {
            get => _unit3;
            set { _unit3 = value; OnPropertyChanged(); }
        }

        public int Unit1Factor
        {
            get => _unit1Factor;
            set
            {
                _unit1Factor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(QuantityInUnit1));
                OnPropertyChanged(nameof(ReorderLevelInUnit1));
            }
        }

        public int Unit2Factor
        {
            get => _unit2Factor;
            set
            {
                _unit2Factor = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(QuantityInUnit2));
                OnPropertyChanged(nameof(ReorderLevelInUnit2));
            }
        }

        public decimal Price1
        {
            get => _price1;
            set { _price1 = value; OnPropertyChanged(); }
        }

        public decimal Price2
        {
            get => _price2;
            set { _price2 = value; OnPropertyChanged(); }
        }

        public decimal Price3
        {
            get => _price3;
            set { _price3 = value; OnPropertyChanged(); }
        }

        public long QuantityInBaseUnit
        {
            get => _quantityInBaseUnit;
            set
            {
                _quantityInBaseUnit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(QuantityInUnit1));
                OnPropertyChanged(nameof(QuantityInUnit2));
                OnPropertyChanged(nameof(QuantityInUnit3));
            }
        }

        // ==================== الخصائص المحسوبة للكمية ====================
        public decimal QuantityInUnit1 => Unit1Factor > 0 ? (decimal)_quantityInBaseUnit / Unit1Factor : 0;
        public decimal QuantityInUnit2 => Unit2Factor > 0 ? (decimal)_quantityInBaseUnit / Unit2Factor : 0;
        public long QuantityInUnit3 => _quantityInBaseUnit;

        public int ReorderLevelInBaseUnit
        {
            get => _reorderLevelInBaseUnit;
            set
            {
                _reorderLevelInBaseUnit = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ReorderLevelInUnit1));
                OnPropertyChanged(nameof(ReorderLevelInUnit2));
                OnPropertyChanged(nameof(ReorderLevelInUnit3));
            }
        }

        public decimal ReorderLevelInUnit1 => Unit1Factor > 0 ? (decimal)_reorderLevelInBaseUnit / Unit1Factor : 0;
        public decimal ReorderLevelInUnit2 => Unit2Factor > 0 ? (decimal)_reorderLevelInBaseUnit / Unit2Factor : 0;
        public int ReorderLevelInUnit3 => _reorderLevelInBaseUnit;

        public string ReorderUnit
        {
            get => _reorderUnit;
            set { _reorderUnit = value; OnPropertyChanged(); }
        }

        public decimal CostPrice
        {
            get => _costPrice;
            set { _costPrice = value; OnPropertyChanged(); }
        }

        public decimal WholesalePrice
        {
            get => _wholesalePrice;
            set { _wholesalePrice = value; OnPropertyChanged(); }
        }

        public long MinQuantity
        {
            get => _minQuantity;
            set { _minQuantity = value; OnPropertyChanged(); }
        }

        public long MaxQuantity
        {
            get => _maxQuantity;
            set { _maxQuantity = value; OnPropertyChanged(); }
        }

        public string Category
        {
            get => _category;
            set { _category = value; OnPropertyChanged(); }
        }

        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(); }
        }

        public DateTime UpdatedAt
        {
            get => _updatedAt;
            set { _updatedAt = value; OnPropertyChanged(); }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region دوال التحويل (Product ↔ ProductItem)

        public Services.ProductItem ToProductItem()
        {
            return new Services.ProductItem
            {
                ProductID = (int)this.Id,
                ProductCode = this.Code ?? "",
                ProductNameAr = this.Name,
                ProductNameEn = "",
                CategoryID = 0,
                CategoryName = this.Category ?? "",
                Unit = this.Unit3 ?? "",
                Barcode = this.Barcode ?? "",
                CostPrice = this.CostPrice,
                SalePrice = this.Price1,
                WholesalePrice = this.WholesalePrice,
                MinimumQuantity = (decimal)this.MinQuantity,
                MaximumQuantity = (decimal)this.MaxQuantity,
                ReorderLevel = (decimal)this.ReorderLevelInBaseUnit,
                IsActive = this.IsActive,
                Unit1 = this.Unit1 ?? "",
                Unit2 = this.Unit2 ?? "",
                Unit3 = this.Unit3 ?? "",
                Unit1Factor = this.Unit1Factor,
                Unit2Factor = this.Unit2Factor,
                Price1 = this.Price1,
                Price2 = this.Price2,
                Price3 = this.Price3,
                ReorderUnit = this.ReorderUnit ?? "",
                ReorderLevelInBaseUnit = this.ReorderLevelInBaseUnit,
                QuantityInBaseUnit = this.QuantityInBaseUnit,
                CurrentQuantity = this.QuantityInUnit3
            };
        }

        public static Product FromProductItem(Services.ProductItem item)
        {
            return new Product
            {
                Id = item.ProductID,
                Code = item.ProductCode,
                Name = item.ProductNameAr,
                Barcode = item.Barcode,
                Unit1 = item.Unit1,
                Unit2 = item.Unit2,
                Unit3 = item.Unit3,
                Unit1Factor = item.Unit1Factor,
                Unit2Factor = item.Unit2Factor,
                Price1 = item.Price1,
                Price2 = item.Price2,
                Price3 = item.Price3,
                QuantityInBaseUnit = item.QuantityInBaseUnit,
                ReorderLevelInBaseUnit = item.ReorderLevelInBaseUnit,
                ReorderUnit = item.ReorderUnit,
                CostPrice = item.CostPrice,
                WholesalePrice = item.WholesalePrice,
                MinQuantity = (long)item.MinimumQuantity,
                MaxQuantity = (long)item.MaximumQuantity,
                Category = item.CategoryName,
                IsActive = item.IsActive,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
        }

        #endregion
    }
}