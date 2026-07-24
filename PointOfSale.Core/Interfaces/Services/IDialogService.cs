using System;

namespace PointOfSale.Core.Interfaces.Services
{
    public interface IDialogService
    {
        bool? ShowDialog<TViewModel>(out TViewModel viewModel) where TViewModel : class;
        bool? ShowDialog<TViewModel>(Action<TViewModel> configureViewModel, out TViewModel viewModel) where TViewModel : class;
    }
}
