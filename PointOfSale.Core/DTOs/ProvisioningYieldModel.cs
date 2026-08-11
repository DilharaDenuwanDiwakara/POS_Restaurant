using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PointOfSale.Core.DTOs
{
    public class ProvisioningYieldModel : INotifyPropertyChanged
    {
        private Func<string, Task<IEnumerable<ProvisioningYieldDetailModel>>> _loadDetailsAsync;
        private Action<System.Exception> _onLoadFailed;
        private bool _detailsLoaded;
        private bool _isExpanded;
        private bool _isLoadingDetails;

        public event PropertyChangedEventHandler PropertyChanged;

        public string ProvisionNumber { get; set; }
        public DateTime ProvisionDate { get; set; }
        public string LocationName { get; set; }
        public string InputProductName { get; set; }
        public decimal InputQty { get; set; }
        public string InputUnit { get; set; }
        public decimal TotalInputCost { get; set; }
        public decimal TotalUsableQty { get; set; }
        public decimal TotalWastageQty { get; set; }
        public decimal YieldPercentage { get; set; }
        public ObservableCollection<ProvisioningYieldDetailModel> Details { get; set; } = new ObservableCollection<ProvisioningYieldDetailModel>();

        public bool IsLoadingDetails
        {
            get => _isLoadingDetails;
            private set
            {
                if (_isLoadingDetails != value)
                {
                    _isLoadingDetails = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    OnPropertyChanged();

                    if (_isExpanded && !_detailsLoaded)
                    {
                        _ = LoadDetailsAsync();
                    }
                }
            }
        }

        public void ConfigureDetailLoader(
            Func<string, Task<IEnumerable<ProvisioningYieldDetailModel>>> loadDetailsAsync,
            Action<System.Exception> onLoadFailed)
        {
            _loadDetailsAsync = loadDetailsAsync;
            _onLoadFailed = onLoadFailed;
        }

        private async Task LoadDetailsAsync()
        {
            if (_loadDetailsAsync == null || IsLoadingDetails)
            {
                return;
            }

            try
            {
                IsLoadingDetails = true;
                Details.Clear();

                var details = await _loadDetailsAsync(ProvisionNumber);
                foreach (var detail in details)
                {
                    Details.Add(detail);
                }

                _detailsLoaded = true;
            }
            catch (System.Exception ex)
            {
                _onLoadFailed?.Invoke(ex);
                Details.Clear();
                _detailsLoaded = true;
            }
            finally
            {
                IsLoadingDetails = false;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
