using System.Windows;

namespace DalView;

public partial class PasswordDialog : Window
{
    public string Password { get; private set; } = string.Empty;

    public PasswordDialog()
    {
        InitializeComponent();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Password = PasswordBox.Password;
        DialogResult = true;
    }
}
