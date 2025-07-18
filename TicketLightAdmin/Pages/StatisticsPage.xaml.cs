using LiveCharts;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace TicketLightAdmin.Pages
{
    public partial class StatisticsPage : Page
    {
        private string connectionString = "Server=TEMHANLAPTOP\\TDG2022;Database=TicketLight2;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        public ChartValues<int> AdminCount { get; set; }
        public ChartValues<int> UserCount { get; set; }
        public ChartValues<int> ApprovedCount { get; set; }
        public ChartValues<int> PendingCount { get; set; }
        public ChartValues<int> RejectedCount { get; set; }
        public ChartValues<double> WalletBalances { get; set; }
        public ChartValues<int> DepositCounts { get; set; }
        public ChartValues<int> WithdrawalCounts { get; set; }
        public string[] UserNames { get; set; }
        public string[] TransactionDates { get; set; }
        public Func<double, string> BalanceFormatter { get; set; }
        public Func<double, string> CountFormatter { get; set; }
        public Func<ChartPoint, string> PointLabel { get; set; }

        public StatisticsPage()
        {
            InitializeComponent();
            LoadStatistics();
            DataContext = this;
        }

        private void LoadStatistics()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();

                    // Пользователи по ролям
                    string userQuery = "SELECT Role, COUNT(*) FROM Users GROUP BY Role";
                    using (SqlCommand cmd = new SqlCommand(userQuery, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        AdminCount = new ChartValues<int> { 0 };
                        UserCount = new ChartValues<int> { 0 };
                        while (reader.Read())
                        {
                            string role = reader.GetString(0);
                            int count = reader.GetInt32(1);
                            if (role == "Admin") AdminCount[0] = count;
                            if (role == "User") UserCount[0] = count;
                        }
                    }

                    // Заявки по статусам
                    string appQuery = "SELECT Status, COUNT(*) FROM Applications GROUP BY Status";
                    using (SqlCommand cmd = new SqlCommand(appQuery, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        ApprovedCount = new ChartValues<int> { 0 };
                        PendingCount = new ChartValues<int> { 0 };
                        RejectedCount = new ChartValues<int> { 0 };
                        while (reader.Read())
                        {
                            string status = reader.GetString(0);
                            int count = reader.GetInt32(1);
                            if (status == "Approved") ApprovedCount[0] = count;
                            if (status == "Pending") PendingCount[0] = count;
                            if (status == "Rejected") RejectedCount[0] = count;
                        }
                    }

                    // Баланс кошельков (первые 5)
                    string walletQuery = "SELECT TOP 5 w.WalletId, w.Balance, u.FullName FROM Wallets w JOIN Users u ON w.UserId = u.UserId";
                    var wallets = new List<(int, double, string)>();
                    using (SqlCommand cmd = new SqlCommand(walletQuery, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            wallets.Add((reader.GetInt32(0), (double)reader.GetDecimal(1), reader.GetString(2)));
                        }
                    }
                    WalletBalances = new ChartValues<double>(wallets.Select(w => w.Item2));
                    UserNames = wallets.Select(w => w.Item3).ToArray();

                    // Транзакции по типам (за последние 7 дней)
                    string transQuery = "SELECT CAST(CreatedAt AS DATE) AS TransactionDate, TransactionType, COUNT(*) " +
                                       "FROM WalletTransactions WHERE CreatedAt >= @StartDate " +
                                       "GROUP BY CAST(CreatedAt AS DATE), TransactionType " +
                                       "ORDER BY CAST(CreatedAt AS DATE)";
                    var transactions = new List<(DateTime, string, int)>();
                    using (SqlCommand cmd = new SqlCommand(transQuery, conn))
                    {
                        cmd.Parameters.AddWithValue("@StartDate", DateTime.Now.AddDays(-7));
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                transactions.Add((reader.GetDateTime(0), reader.GetString(1), reader.GetInt32(2)));
                            }
                        }
                    }

                    var dates = transactions.Select(t => t.Item1).Distinct().OrderBy(d => d).ToList();
                    TransactionDates = dates.Select(d => d.ToString("dd.MM")).ToArray();
                    DepositCounts = new ChartValues<int>(dates.Select(d => transactions.Where(t => t.Item1 == d && t.Item2 == "Deposit").Sum(t => t.Item3)));
                    WithdrawalCounts = new ChartValues<int>(dates.Select(d => transactions.Where(t => t.Item1 == d && t.Item2 == "Withdrawal").Sum(t => t.Item3)));

                    // Форматтеры
                    BalanceFormatter = value => $"{value:N0} ₸";
                    CountFormatter = value => $"{value:N0}";
                    PointLabel = chartPoint => $"{chartPoint.Y}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки статистики: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Chart_OnDataClick(object sender, ChartPoint chartPoint)
        {
            MessageBox.Show($"Выбрано: {chartPoint.SeriesView.Title} ({chartPoint.Y})", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}