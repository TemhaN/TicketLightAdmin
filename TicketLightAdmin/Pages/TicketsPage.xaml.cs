using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using Microsoft.VisualBasic;
using QRCoder;
using System.Drawing.Imaging;
using System.Windows.Navigation;

namespace TicketLightAdmin.Pages
{
    public partial class TicketsPage : Page
    {
        private string connectionString = "Server=TEMHANLAPTOP\\TDG2022;Database=TicketLight;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        private List<Ticket> tickets = new List<Ticket>();
        private List<User> users = new List<User>();

        public TicketsPage()
        {
            InitializeComponent();
            this.Loaded += TicketsPage_Loaded; // Привязываем событие Loaded
        }

        private void TicketsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTickets(); // Загружаем билеты
            LoadUsersWithApplications(); // Загружаем пользователей с заявками
        }

        // 🔹 Загрузка билетов
        public void LoadTickets()
        {
            tickets.Clear();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT TicketId, ApplicationId, QRCode, Barcode, ExpiryDate FROM Tickets";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        tickets.Add(new Ticket
                        {
                            TicketId = reader.GetInt32(0),
                            ApplicationId = reader.GetInt32(1),
                            QRCode = reader.GetString(2),
                            Barcode = reader.GetString(3),
                            ExpiryDate = reader.GetDateTime(4)
                        });
                    }
                }
            }

            TicketsDataGrid.ItemsSource = null;
            TicketsDataGrid.ItemsSource = tickets;
        }

        // 🔹 Загрузка пользователей для выпадающего списка (исключая тех, у кого заявка принята)
        private void LoadUsersWithApplications()
        {
            UsersComboBox.Items.Clear();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = @"
            SELECT DISTINCT u.UserId, u.FullName, u.Email, u.PhoneNumber, u.Role 
            FROM Users u
            JOIN Applications a ON u.UserId = a.UserId
            WHERE a.Status <> N'Принят'"; // 🔹 Исключаем заявки со статусом 'Принят'

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        UsersComboBox.Items.Add(new User
                        {
                            UserId = reader.GetInt32(0),
                            FullName = reader.GetString(1),
                            Email = reader.GetString(2),
                            PhoneNumber = reader.GetString(3),
                            Role = reader.GetString(4)
                        });
                    }
                }
            }
        }

        private int? GetApplicationId(int userId)
        {
            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT TOP 1 ApplicationId FROM Applications WHERE UserId = @UserId ORDER BY SubmissionDate DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    object result = cmd.ExecuteScalar();
                    return result != null ? (int?)result : null;
                }
            }
        }



        // 🔹 Генерация билета
        private void GenerateTicket_Click(object sender, RoutedEventArgs e)
        {
            if (UsersComboBox.SelectedItem is User selectedUser)
            {
                int? applicationId = GetApplicationId(selectedUser.UserId);
                if (applicationId == null)
                {
                    MessageBox.Show($"У пользователя {selectedUser.FullName} нет заявки!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    using (SqlTransaction transaction = conn.BeginTransaction()) // 🔹 Транзакция
                    {
                        try
                        {
                            // 1️⃣ Получаем CategoryId из заявки
                            string getCategoryQuery = "SELECT CategoryId FROM Applications WHERE ApplicationId = @ApplicationId";
                            int? categoryId = null;
                            using (SqlCommand cmd = new SqlCommand(getCategoryQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ApplicationId", applicationId);
                                object result = cmd.ExecuteScalar();
                                if (result != null) categoryId = Convert.ToInt32(result);
                            }

                            // 2️⃣ Получаем CategoryName из BenefitCategories
                            string categoryName = null;
                            if (categoryId.HasValue)
                            {
                                string getCategoryNameQuery = "SELECT CategoryName FROM BenefitCategories WHERE CategoryId = @CategoryId";
                                using (SqlCommand cmd = new SqlCommand(getCategoryNameQuery, conn, transaction))
                                {
                                    cmd.Parameters.AddWithValue("@CategoryId", categoryId);
                                    object result = cmd.ExecuteScalar();
                                    if (result != null) categoryName = result.ToString();
                                }
                            }

                            if (string.IsNullOrEmpty(categoryName))
                            {
                                MessageBox.Show("Ошибка: Не удалось определить категорию!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                transaction.Rollback();
                                return;
                            }

                            // 3️⃣ Обновляем роль пользователя в Users
                            string updateUserRoleQuery = "UPDATE Users SET Role = @Role WHERE UserId = @UserId";
                            using (SqlCommand cmd = new SqlCommand(updateUserRoleQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@Role", categoryName);
                                cmd.Parameters.AddWithValue("@UserId", selectedUser.UserId);
                                cmd.ExecuteNonQuery();
                            }

                            // 4️⃣ Проверяем, есть ли уже билет
                            string checkTicketQuery = "SELECT COUNT(*) FROM Tickets WHERE ApplicationId = @ApplicationId";
                            int ticketCount = 0;
                            using (SqlCommand cmd = new SqlCommand(checkTicketQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ApplicationId", applicationId);
                                ticketCount = (int)cmd.ExecuteScalar();
                            }

                            if (ticketCount > 0)
                            {
                                MessageBox.Show($"Билет для {selectedUser.FullName} уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                                transaction.Rollback();
                                return;
                            }

                            // 5️⃣ Генерируем новый билет
                            string qrData = $"{selectedUser.FullName}|{selectedUser.Email}|{selectedUser.PhoneNumber}|{categoryName}";
                            string barcode = GenerateRandomBarcode();
                            DateTime expiryDate = DateTime.Now.AddMonths(6);

                            string insertTicketQuery = @"
                    INSERT INTO Tickets (ApplicationId, QRCode, Barcode, ExpiryDate)
                    VALUES (@ApplicationId, @QRCode, @Barcode, @ExpiryDate)";

                            using (SqlCommand cmd = new SqlCommand(insertTicketQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ApplicationId", applicationId);
                                cmd.Parameters.AddWithValue("@QRCode", qrData);
                                cmd.Parameters.AddWithValue("@Barcode", barcode);
                                cmd.Parameters.AddWithValue("@ExpiryDate", expiryDate);
                                cmd.ExecuteNonQuery();
                            }

                            // 6️⃣ Обновляем статус заявки (если ещё не "Принят")
                            string updateApplicationQuery = @"
                    UPDATE Applications
                    SET ApprovalDate = GETDATE(),
                        Status = CASE WHEN Status != N'Принят' THEN N'Принят' ELSE Status END
                    WHERE ApplicationId = @ApplicationId";

                            using (SqlCommand cmd = new SqlCommand(updateApplicationQuery, conn, transaction))
                            {
                                cmd.Parameters.AddWithValue("@ApplicationId", applicationId);
                                cmd.ExecuteNonQuery();
                            }

                            transaction.Commit(); // 🔹 Подтверждаем транзакцию
                            MessageBox.Show($"Билет для {selectedUser.FullName} создан!\nСтатус заявки: 'Принят'\nРоль обновлена: {categoryName}",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                            LoadUsersWithApplications();
                        }
                        catch (Exception ex)
                        {
                            transaction.Rollback(); // 🔹 Откат в случае ошибки
                            MessageBox.Show($"Ошибка при создании билета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }

                LoadTickets();
            }
            else
            {
                MessageBox.Show("Выберите пользователя!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }







        // 🔹 Показать QR-код
        private void ViewQRCode_Click(object sender, RoutedEventArgs e)
        {
            if (TicketsDataGrid.SelectedItem is Ticket selectedTicket)
            {
                string qrData = selectedTicket.QRCode?.Trim();
                if (string.IsNullOrEmpty(qrData))
                {
                    MessageBox.Show("QR-код пустой!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверяем структуру данных
                string[] userData = qrData.Split('|');
                //MessageBox.Show($"Получено частей: {userData.Length}", "Отладка", MessageBoxButton.OK, MessageBoxImage.Information);

                if (userData.Length != 4)
                {
                    MessageBox.Show("Некорректные данные QR-кода. Ожидалось 4 части!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string fullName = userData[0].Trim();
                string email = userData[1].Trim();
                string phone = userData[2].Trim();
                string role = userData[3].Trim();

                BitmapImage qrImage = GenerateQRCode(qrData);
                if (qrImage == null)
                {
                    MessageBox.Show("Ошибка при генерации QR-кода", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                QRCodeWindow qrWindow = new TicketLightAdmin.Pages.QRCodeWindow(qrImage, fullName, email, phone, role);
                qrWindow.ShowDialog();
            }
            else
            {
                MessageBox.Show("Выберите билет!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }



        // 🔹 Генерация изображения QR-кода
        private BitmapImage GenerateQRCode(string qrData)
        {
            try
            {
                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(qrData, QRCodeGenerator.ECCLevel.Q);
                QRCode qrCode = new QRCode(qrCodeData);

                using (Bitmap qrBitmap = qrCode.GetGraphic(20))
                using (MemoryStream ms = new MemoryStream())
                {
                    qrBitmap.Save(ms, ImageFormat.Png);
                    ms.Position = 0;

                    BitmapImage bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = ms;
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.EndInit();

                    return bitmapImage;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при генерации QR-кода: " + ex.Message);
                return null;
            }
        }


        private ImageSource BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                memory.Position = 0;

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();

                return bitmapImage;
            }
        }



        // 🔹 Генерация случайного штрихкода (12 цифр)
        private string GenerateRandomBarcode()
        {
            Random random = new Random();
            StringBuilder barcode = new StringBuilder();
            for (int i = 0; i < 12; i++)
            {
                barcode.Append(random.Next(0, 10));
            }
            return barcode.ToString();
        }

        // 🔹 Редактирование билета
        private void EditTicket_Click(object sender, RoutedEventArgs e)
        {
            if (TicketsDataGrid.SelectedItem is Ticket selectedTicket)
            {
                string newQRCode = Interaction.InputBox("Введите новый QR-код:", "Редактирование", selectedTicket.QRCode);
                string newBarcode = Interaction.InputBox("Введите новый штрихкод:", "Редактирование", selectedTicket.Barcode);
                string newExpiryDateStr = Interaction.InputBox("Введите новую дату истечения (YYYY-MM-DD):", "Редактирование", selectedTicket.ExpiryDate.ToString("yyyy-MM-dd"));

                if (DateTime.TryParse(newExpiryDateStr, out DateTime newExpiryDate))
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string query = "UPDATE Tickets SET QRCode=@QRCode, Barcode=@Barcode, ExpiryDate=@ExpiryDate WHERE TicketId=@TicketId";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@QRCode", string.IsNullOrWhiteSpace(newQRCode) ? selectedTicket.QRCode : newQRCode);
                            cmd.Parameters.AddWithValue("@Barcode", string.IsNullOrWhiteSpace(newBarcode) ? selectedTicket.Barcode : newBarcode);
                            cmd.Parameters.AddWithValue("@ExpiryDate", newExpiryDate);
                            cmd.Parameters.AddWithValue("@TicketId", selectedTicket.TicketId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    LoadTickets();
                }
                else
                {
                    MessageBox.Show("Некорректный формат даты!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        // 🔹 Удаление билета
        // 🔹 Удаление билета и связанной заявки
        private void DeleteTicket_Click(object sender, RoutedEventArgs e)
        {
            if (TicketsDataGrid.SelectedItem is Ticket selectedTicket)
            {
                if (MessageBox.Show($"Удалить билет {selectedTicket.TicketId} и связанную заявку?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        // 1️⃣ Получаем ApplicationId, связанный с этим билетом
                        int applicationId = -1;
                        string getAppQuery = "SELECT ApplicationId FROM Tickets WHERE TicketId = @TicketId";

                        using (SqlCommand getAppCmd = new SqlCommand(getAppQuery, conn))
                        {
                            getAppCmd.Parameters.AddWithValue("@TicketId", selectedTicket.TicketId);
                            object result = getAppCmd.ExecuteScalar();
                            if (result != null)
                                applicationId = Convert.ToInt32(result);
                        }

                        // 2️⃣ Удаляем билет
                        string deleteTicketQuery = "DELETE FROM Tickets WHERE TicketId = @TicketId";
                        using (SqlCommand deleteTicketCmd = new SqlCommand(deleteTicketQuery, conn))
                        {
                            deleteTicketCmd.Parameters.AddWithValue("@TicketId", selectedTicket.TicketId);
                            deleteTicketCmd.ExecuteNonQuery();
                        }

                        // 3️⃣ Удаляем заявку, если она была найдена
                        if (applicationId != -1)
                        {
                            string deleteAppQuery = "DELETE FROM Applications WHERE ApplicationId = @ApplicationId";
                            using (SqlCommand deleteAppCmd = new SqlCommand(deleteAppQuery, conn))
                            {
                                deleteAppCmd.Parameters.AddWithValue("@ApplicationId", applicationId);
                                deleteAppCmd.ExecuteNonQuery();
                            }
                        }
                    }

                    LoadTickets();
                }
            }
        }


        public class Ticket
        {
            public int TicketId { get; set; }
            public int ApplicationId { get; set; }
            public string QRCode { get; set; }
            public string Barcode { get; set; }
            public DateTime ExpiryDate { get; set; }
        }
    }
}
