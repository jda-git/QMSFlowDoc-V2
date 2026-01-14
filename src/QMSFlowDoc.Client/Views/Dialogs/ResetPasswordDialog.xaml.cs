using Microsoft.UI.Xaml.Controls;

namespace QMSFlowDoc.Client.Views.Dialogs;

public sealed partial class ResetPasswordDialog : ContentDialog
{
    public string NewPassword => NewPasswordBox.Password;

    public ResetPasswordDialog()
    {
        this.InitializeComponent();
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ErrorTextBlock.Text = string.Empty;

        if (string.IsNullOrWhiteSpace(NewPasswordBox.Password))
        {
            ErrorTextBlock.Text = "La nueva contraseña es requerida.";
            args.Cancel = true;
            return;
        }

        if (NewPasswordBox.Password.Length < 6)
        {
            ErrorTextBlock.Text = "La nueva contraseña debe tener al menos 6 caracteres.";
            args.Cancel = true;
            return;
        }

        if (NewPasswordBox.Password != ConfirmPasswordBox.Password)
        {
            ErrorTextBlock.Text = "Las nuevas contraseñas no coinciden.";
            args.Cancel = true;
            return;
        }
    }
}
