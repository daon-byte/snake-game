using System;
using System.Windows;
using System.Windows.Input;
using snake_game.Game;

namespace snake_game
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            View.Focus();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (View.HandleKey(e.Key)) e.Handled = true;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            View.PauseIfRunning();
        }
    }
}
