using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TicketLightAdmin.Pages
{
    public partial class WalletTransactionsPage : Page
    {
        private string connectionString = "Server=TEMHANLAPTOP\\TDG2022;Database=TicketLight2;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        private List<WalletTransaction> transactions = new List<WalletTransaction>();

        public WalletTransactionsPage()
        {
            InitializeComponent();
            this.Loaded += WalletTransactionsPage_Loaded;
            TransactionTypeComboBox.SelectionChanged += TransactionTypeComboBox_SelectionChanged;
        }

        private void WalletTransactionsPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadTransactions();
            LoadWallets();
        }

        private void LoadTransactions()
        {
            transactions.Clear();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT t.TransactionId, t.WalletId, t.Amount, t.TransactionType, t.CreatedAt, u.FullName " +
                                   "FROM WalletTransactions t JOIN Wallets w ON t.WalletId = w.WalletId " +
                                   "JOIN Users u ON w.UserId = u.UserId";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            transactions.Add(new WalletTransaction
                            {
                                TransactionId = reader.GetInt32(0),
                                WalletId = reader.GetInt32(1),
                                Amount = reader.GetDecimal(2),
                                TransactionType = reader.GetString(3),
                                CreatedAt = reader.GetDateTime(4),
                                UserName = reader.IsDBNull(5) ? null : reader.GetString(5)
                            });
                        }
                    }
                }
                TransactionsDataGrid.ItemsSource = null;
                TransactionsDataGrid.ItemsSource = transactions;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки транзакций: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadWallets()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT w.WalletId, u.FullName FROM Wallets w JOIN Users u ON w.UserId = u.UserId";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        var wallets = new List<Wallet>();
                        while (reader.Read())
                        {
                            wallets.Add(new Wallet
                            {
                                WalletId = reader.GetInt32(0),
                                UserName = reader.IsDBNull(1) ? null : reader.GetString(1)
                            });
                        }
                        WalletsComboBox.ItemsSource = wallets;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки кошельков: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterTransactions();
        }

        private void TransactionTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            FilterTransactions();
        }

        private void FilterTransactions()
        {
            var searchText = SearchTextBox.Text.ToLower();
            var selectedType = (TransactionTypeComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            var selectedWalletId = WalletsComboBox.SelectedValue as int?;

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT t.TransactionId, t.WalletId, t.Amount, t.TransactionType, t.CreatedAt, u.FullName " +
                                   "FROM WalletTransactions t JOIN Wallets w ON t.WalletId = w.WalletId " +
                                   "JOIN Users u ON w.UserId = u.UserId WHERE 1=1";
                    if (!string.IsNullOrEmpty(searchText))
                        query += " AND u.FullName LIKE @SearchText";
                    if (selectedType != "Все" && selectedType != null)
                        query += " AND t.TransactionType = @TransactionType";
                    if (selectedWalletId.HasValue)
                        query += " AND t.WalletId = @WalletId";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        if (!string.IsNullOrEmpty(searchText))
                            cmd.Parameters.AddWithValue("@SearchText", $"%{searchText}%");
                        if (selectedType != "Все" && selectedType != null)
                            cmd.Parameters.AddWithValue("@TransactionType", selectedType == "Пополнение" ? "Deposit" : "Withdrawal");
                        if (selectedWalletId.HasValue)
                            cmd.Parameters.AddWithValue("@WalletId", selectedWalletId.Value);

                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            transactions.Clear();
                            while (reader.Read())
                            {
                                transactions.Add(new WalletTransaction
                                {
                                    TransactionId = reader.GetInt32(0),
                                    WalletId = reader.GetInt32(1),
                                    Amount = reader.GetDecimal(2),
                                    TransactionType = reader.GetString(3),
                                    CreatedAt = reader.GetDateTime(4),
                                    UserName = reader.IsDBNull(5) ? null : reader.GetString(5)
                                });
                            }
                        }
                    }
                }
                TransactionsDataGrid.ItemsSource = null;
                TransactionsDataGrid.ItemsSource = transactions;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка фильтрации транзакций: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteTransaction_Click(object sender, RoutedEventArgs e)
        {
            if (TransactionsDataGrid.SelectedItem is WalletTransaction selectedTransaction)
            {
                if (MessageBox.Show($"Удалить транзакцию ID: {selectedTransaction.TransactionId}?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                            string deleteQuery = "DELETE FROM WalletTransactions WHERE TransactionId = @TransactionId";
                            using (SqlCommand cmd = new SqlCommand(deleteQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@TransactionId", selectedTransaction.TransactionId);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        LoadTransactions();
                        MessageBox.Show("Транзакция удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления транзакции: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }

    public class WalletTransaction
    {
        public int TransactionId { get; set; }
        public int WalletId { get; set; }
        public decimal Amount { get; set; }
        public string TransactionType { get; set; }
        public DateTime CreatedAt { get; set; }
        public string UserName { get; set; }
    }
}