using System.Windows;

namespace GLOptimizer.App.Services;

public sealed class MessageBoxConfirmation : IUserConfirmation
{
    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.None, MessageBoxResult.No) == MessageBoxResult.Yes;
}