using Avalonia.Controls;
using Avalonia.Input;
using QuicFlowClient.ViewModels;
using System;
using System.Reactive.Linq;
using System.Windows.Input;

namespace QuicFlowClient.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void OnInputKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (DataContext is MainWindowViewModel vm)
                {
                    ICommand cmd = vm.SendMessageCommand;
                    if (cmd.CanExecute(null))
                    {
                        vm.SendMessageCommand.Execute(System.Reactive.Unit.Default).Subscribe();
                        e.Handled = true;
                    }
                }
            }
        }
    }
}
