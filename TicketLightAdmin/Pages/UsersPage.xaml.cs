using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;
using System.Security.Cryptography;

namespace TicketLightAdmin.Pages
{
    public partial class UsersPage : Page
    {
        private string connectionString = "Server=TEMHANLAPTOP\\TDG2022;Database=TicketLight2;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        private List<User> users = new List<User>();

        public UsersPage()
        {
            InitializeComponent();
            this.Loaded += UsersPage_Loaded; // Привязываем событие Loaded
        }

        private void UsersPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadUsers();
        }

        // Загрузка пользователей из БД
        public void LoadUsers()
        {
            users.Clear();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "SELECT UserId, FullName, Email, PhoneNumber, IIN, Role, RegistrationDate FROM Users";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        users.Add(new User
                        {
                            UserId = reader.GetInt32(0),
                            FullName = reader.IsDBNull(1) ? null : reader.GetString(1),
                            Email = reader.IsDBNull(2) ? null : reader.GetString(2), 
                            PhoneNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                            IIN = reader.IsDBNull(4) ? null : reader.GetString(4),
                            Role = reader.IsDBNull(5) ? null : reader.GetString(5), 
                            RegistrationDate = reader.GetDateTime(6)
                        });
                    }
                }
            }

            UsersDataGrid.ItemsSource = null;
            UsersDataGrid.ItemsSource = users;
        }
        // Редактирование пользователя
        private void EditUser_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is User selectedUser)
            {
                string newName = Interaction.InputBox("Введите новое имя:", "Редактирование", selectedUser.FullName);
                string newEmail = Interaction.InputBox("Введите новый Email:", "Редактирование", selectedUser.Email);
                string newPhone = Interaction.InputBox("Введите новый телефон:", "Редактирование", selectedUser.PhoneNumber);
                string newIIN = Interaction.InputBox("Введите новый IIN:", "Редактирование", selectedUser.IIN); // Добавляем ввод IIN
                string newRole = Interaction.InputBox("Введите новую роль (User/Admin):", "Редактирование", selectedUser.Role);
                string newPassword = Interaction.InputBox("Введите новый пароль (оставьте пустым, если не менять):", "Редактирование", "");

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "UPDATE Users SET FullName=@FullName, Email=@Email, PhoneNumber=@PhoneNumber, IIN=@IIN, Role=@Role WHERE UserId=@UserId";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@FullName", string.IsNullOrWhiteSpace(newName) ? selectedUser.FullName : newName);
                        cmd.Parameters.AddWithValue("@Email", string.IsNullOrWhiteSpace(newEmail) ? selectedUser.Email : newEmail);
                        cmd.Parameters.AddWithValue("@PhoneNumber", string.IsNullOrWhiteSpace(newPhone) ? selectedUser.PhoneNumber : newPhone);
                        cmd.Parameters.AddWithValue("@IIN", string.IsNullOrWhiteSpace(newIIN) ? selectedUser.IIN : newIIN); // Добавляем IIN
                        cmd.Parameters.AddWithValue("@Role", string.IsNullOrWhiteSpace(newRole) ? selectedUser.Role : newRole);
                        cmd.Parameters.AddWithValue("@UserId", selectedUser.UserId);
                        cmd.ExecuteNonQuery();
                    }

                    // Обновляем пароль, если он был введён
                    if (!string.IsNullOrWhiteSpace(newPassword))
                    {
                        string hashedPassword = HashPassword(newPassword);
                        string updatePasswordQuery = "UPDATE Users SET PasswordHash=@PasswordHash WHERE UserId=@UserId";

                        using (SqlCommand cmd = new SqlCommand(updatePasswordQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                            cmd.Parameters.AddWithValue("@UserId", selectedUser.UserId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                }

                LoadUsers();
            }
        }

        // Метод для хеширования пароля
        private string HashPassword(string password)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                StringBuilder builder = new StringBuilder();
                foreach (byte b in hashedBytes)
                {
                    builder.Append(b.ToString("x2"));
                }
                return builder.ToString();
            }
        }

        // Удаление пользователя
        private void DeleteUser_Click(object sender, RoutedEventArgs e)
        {
            if (UsersDataGrid.SelectedItem is User selectedUser)
            {
                if (MessageBox.Show($"Удалить {selectedUser.FullName}?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();

                        // Проверяем, есть ли связанные записи в Applications
                        string checkQuery = "SELECT COUNT(*) FROM Applications WHERE UserId = @UserId";
                        using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                        {
                            checkCmd.Parameters.AddWithValue("@UserId", selectedUser.UserId);
                            int count = (int)checkCmd.ExecuteScalar();

                            if (count > 0)
                            {
                                MessageBox.Show("Нельзя удалить пользователя, так как у него есть заявки!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                return;
                            }
                        }

                        // Если ссылок нет, удаляем пользователя
                        string deleteQuery = "DELETE FROM Users WHERE UserId = @UserId";
                        using (SqlCommand cmd = new SqlCommand(deleteQuery, conn))
                        {
                            cmd.Parameters.AddWithValue("@UserId", selectedUser.UserId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    LoadUsers();
                }
            }
        }
    }

    public class User
    {
        public int UserId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string IIN { get; set; }
        public string Role { get; set; }
        public DateTime RegistrationDate { get; set; }
    }
}