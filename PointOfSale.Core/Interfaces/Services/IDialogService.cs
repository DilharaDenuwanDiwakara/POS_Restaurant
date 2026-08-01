using System;

namespace PointOfSale.Core.Interfaces.Services
{
    public enum DialogMessageType
    {
        Information,
        Warning,
        Error
    }

    public interface IDialogService
    {
        bool? ShowDialog<TViewModel>(out TViewModel viewModel) where TViewModel : class;
        bool? ShowDialog<TViewModel>(Action<TViewModel> configureViewModel, out TViewModel viewModel) where TViewModel : class;
        void ShowMessage(string message, string title, DialogMessageType messageType = DialogMessageType.Information);
    }
}
