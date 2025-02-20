using System.Windows;
using System.Windows.Media.Imaging;


namespace TicketLightAdmin.Pages // Было Windows, заменяем на Pages
{
    public partial class QRCodeWindow : Window
    {
        public QRCodeWindow(BitmapImage qrImage, string fullName, string email, string phone, string role)
        {
            InitializeComponent();
            QRCodeImage.Source = qrImage;
            UserNameText.Text = $"Имя: {fullName}";
            UserEmailText.Text = $"Email: {email}";
            UserPhoneText.Text = $"Телефон: {phone}";
            UserRoleText.Text = $"Роль: {role}";
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}
