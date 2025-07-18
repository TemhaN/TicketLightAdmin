using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;

namespace TicketLightAdmin.Pages
{
    public partial class WalletsPage : Page
    {
        private string connectionString = "Server=TEMHANLAPTOP\\TDG2022;Database=TicketLight2;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        private List<Wallet> wallets = new List<Wallet>();

        public WalletsPage()
        {
            InitializeComponent();
            this.Loaded += WalletsPage_Loaded;
        }

        private void WalletsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadWallets();
            LoadUsers();
        }

        private void LoadWallets()
        {
            wallets.Clear();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT w.WalletId, w.UserId, w.Balance, w.CreatedAt, u.FullName " +
                                   "FROM Wallets w JOIN Users u ON w.UserId = u.UserId";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            wallets.Add(new Wallet
                            {
                                WalletId = reader.GetInt32(0),
                                UserId = reader.GetInt32(1),
                                Balance = reader.GetDecimal(2),
                                CreatedAt = reader.GetDateTime(3),
                                UserName = reader.IsDBNull(4) ? null : reader.GetString(4)
                            });
                        }
                    }
                }
                WalletsDataGrid.ItemsSource = null;
                WalletsDataGrid.ItemsSource = wallets;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки кошельков: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadUsers()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT UserId, FullName FROM Users";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        var users = new List<User>();
                        while (reader.Read())
                        {
                            users.Add(new User
                            {
                                UserId = reader.GetInt32(0),
                                FullName = reader.IsDBNull(1) ? null : reader.GetString(1)
                            });
                        }
                        UsersComboBox.ItemsSource = users;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки пользователей: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = SearchTextBox.Text.ToLower();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT w.WalletId, w.UserId, w.Balance, w.CreatedAt, u.FullName " +
                                   "FROM Wallets w JOIN Users u ON w.UserId = u.UserId " +
                                   "WHERE u.FullName LIKE @SearchText";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@SearchText", $"%{searchText}%");
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            wallets.Clear();
                            while (reader.Read())
                            {
                                wallets.Add(new Wallet
                                {
                                    WalletId = reader.GetInt32(0),
                                    UserId = reader.GetInt32(1),
                                    Balance = reader.GetDecimal(2),
                                    CreatedAt = reader.GetDateTime(3),
                                    UserName = reader.IsDBNull(4) ? null : reader.GetString(4)
                                });
                            }
                        }
                    }
                }
                WalletsDataGrid.ItemsSource = null;
                WalletsDataGrid.ItemsSource = wallets;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка поиска: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CreateWallet_Click(object sender, RoutedEventArgs e)
        {
            if (UsersComboBox.SelectedValue == null)
            {
                MessageBox.Show("Выберите пользователя!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO Wallets (UserId, Balance, CreatedAt) VALUES (@UserId, @Balance, @CreatedAt)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@UserId", (int)UsersComboBox.SelectedValue);
                        cmd.Parameters.AddWithValue("@Balance", 0);
                        cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }
                LoadWallets();
                MessageBox.Show("Кошелек создан!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка создания кошелька: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditWallet_Click(object sender, RoutedEventArgs e)
        {
            if (WalletsDataGrid.SelectedItem is Wallet selectedWallet)
            {
                string newBalance = Interaction.InputBox("Введите новый баланс:", "Редактирование", selectedWallet.Balance.ToString());
                if (decimal.TryParse(newBalance, out decimal balance))
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                            string query = "UPDATE Wallets SET Balance=@Balance WHERE WalletId=@WalletId";
                            using (SqlCommand cmd = new SqlCommand(query, conn))
                            {
                                cmd.Parameters.AddWithValue("@Balance", balance);
                                cmd.Parameters.AddWithValue("@WalletId", selectedWallet.WalletId);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        LoadWallets();
                        MessageBox.Show("Баланс обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка редактирования кошелька: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    MessageBox.Show("Некорректный баланс!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void DeleteWallet_Click(object sender, RoutedEventArgs e)
        {
            if (WalletsDataGrid.SelectedItem is Wallet selectedWallet)
            {
                if (MessageBox.Show($"Удалить кошелек для {selectedWallet.UserName}?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                            string checkQuery = "SELECT COUNT(*) FROM WalletTransactions WHERE WalletId = @WalletId";
                            using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                            {
                                checkCmd.Parameters.AddWithValue("@WalletId", selectedWallet.WalletId);
                                int count = (int)checkCmd.ExecuteScalar();
                                if (count > 0)
                                {
                                    MessageBox.Show("Нельзя удалить кошелек, так как у него есть транзакции!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    return;
                                }
                            }

                            string deleteQuery = "DELETE FROM Wallets WHERE WalletId = @WalletId";
                            using (SqlCommand cmd = new SqlCommand(deleteQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@WalletId", selectedWallet.WalletId);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        LoadWallets();
                        MessageBox.Show("Кошелек удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления кошелька: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }

    public class Wallet
    {
        public int WalletId { get; set; }
        public int UserId { get; set; }
        public decimal Balance { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UserName { get; set; }
    }

}