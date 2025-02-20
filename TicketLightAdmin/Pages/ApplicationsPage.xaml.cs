﻿using System;
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

namespace TicketLightAdmin.Pages
{
    public partial class ApplicationsPage : Page
    {
        private string connectionString = "Server=TEMHANLAPTOP\\TDG2022;Database=TicketLight;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        private List<ApplicationModel> applications = new List<ApplicationModel>();

        public ApplicationsPage()
        {
            InitializeComponent();
            LoadApplications();
        }

        // 🔹 Загрузка заявок из БД
        private void LoadApplications()
        {
            applications.Clear();

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = @"
                    SELECT a.ApplicationId, u.FullName, b.CategoryName, a.Status, a.SubmissionDate, a.ApprovalDate
                    FROM Applications a
                    JOIN Users u ON a.UserId = u.UserId
                    JOIN BenefitCategories b ON a.CategoryId = b.CategoryId
                    WHERE a.Status <> 'Принято'";  // Исключаем уже одобренные заявки

                using (SqlCommand cmd = new SqlCommand(query, conn))
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        applications.Add(new ApplicationModel
                        {
                            ApplicationId = reader.GetInt32(0),
                            UserName = reader.GetString(1),
                            CategoryName = reader.GetString(2),
                            Status = reader.GetString(3),
                            SubmissionDate = reader.GetDateTime(4),
                            ApprovalDate = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5)
                        });
                    }
                }
            }

            ApplicationsDataGrid.ItemsSource = null;
            ApplicationsDataGrid.ItemsSource = applications;
        }

        // 🔹 Одобрение заявки
        private void ApproveApplication_Click(object sender, RoutedEventArgs e)
        {
            if (ApplicationsDataGrid.SelectedItem is ApplicationModel selectedApplication)
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    SqlTransaction transaction = conn.BeginTransaction();

                    try
                    {
                        // 🔸 Обновляем статус заявки
                        string updateQuery = "UPDATE Applications SET Status='Принято', ApprovalDate=@ApprovalDate WHERE ApplicationId=@ApplicationId";
                        using (SqlCommand cmd = new SqlCommand(updateQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ApprovalDate", DateTime.Now);
                            cmd.Parameters.AddWithValue("@ApplicationId", selectedApplication.ApplicationId);
                            cmd.ExecuteNonQuery();
                        }

                        // 🔸 Получаем UserId и CategoryId
                        int userId = 0, categoryId = 0;
                        string getUserAndCategoryQuery = "SELECT UserId, CategoryId FROM Applications WHERE ApplicationId = @ApplicationId";
                        using (SqlCommand cmd = new SqlCommand(getUserAndCategoryQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ApplicationId", selectedApplication.ApplicationId);
                            using (SqlDataReader reader = cmd.ExecuteReader())
                            {
                                if (reader.Read())
                                {
                                    userId = reader.GetInt32(0);
                                    categoryId = reader.GetInt32(1);
                                }
                            }
                        }

                        // 🔸 Получаем название категории
                        string categoryName = null;
                        string getCategoryQuery = "SELECT CategoryName FROM BenefitCategories WHERE CategoryId = @CategoryId";
                        using (SqlCommand cmd = new SqlCommand(getCategoryQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@CategoryId", categoryId);
                            object result = cmd.ExecuteScalar();
                            if (result != null) categoryName = result.ToString();
                        }

                        if (string.IsNullOrEmpty(categoryName))
                        {
                            MessageBox.Show("Ошибка: Не удалось определить категорию!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            transaction.Rollback();
                            return;
                        }

                        // 🔸 Обновляем роль пользователя
                        string updateUserRoleQuery = "UPDATE Users SET Role = @Role WHERE UserId = @UserId";
                        using (SqlCommand cmd = new SqlCommand(updateUserRoleQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@Role", categoryName);
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.ExecuteNonQuery();
                        }

                        // 🔸 Генерация билета (QR-код и штрихкод)
                        string barcode = GenerateRandomBarcode();
                        string qrData = $"{selectedApplication.UserName}|{categoryName}";
                        DateTime expiryDate = DateTime.Now.AddMonths(6);

                        string insertTicketQuery = @"
                    INSERT INTO Tickets (ApplicationId, QRCode, Barcode, ExpiryDate)
                    VALUES (@ApplicationId, @QRCode, @Barcode, @ExpiryDate)";

                        using (SqlCommand cmd = new SqlCommand(insertTicketQuery, conn, transaction))
                        {
                            cmd.Parameters.AddWithValue("@ApplicationId", selectedApplication.ApplicationId);
                            cmd.Parameters.AddWithValue("@QRCode", qrData);
                            cmd.Parameters.AddWithValue("@Barcode", barcode);
                            cmd.Parameters.AddWithValue("@ExpiryDate", expiryDate);
                            cmd.ExecuteNonQuery();
                        }

                        transaction.Commit(); // ✅ Подтверждаем изменения

                        MessageBox.Show($"Билет для {selectedApplication.UserName} создан!\nКатегория: {categoryName}\nДействует до: {expiryDate.ToShortDateString()}",
                            "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                        LoadApplications();
                        TicketsPage ticketsPage = new TicketsPage();
                        ticketsPage.LoadTickets();
                        UsersPage usersPage = new UsersPage();
                        usersPage.LoadUsers();
                    }
                    catch (Exception ex)
                    {
                        transaction.Rollback();
                        MessageBox.Show($"Ошибка при одобрении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }


        // 🔹 Удаление заявки
        private void DeleteApplication_Click(object sender, RoutedEventArgs e)
        {
            if (ApplicationsDataGrid.SelectedItem is ApplicationModel selectedApplication)
            {
                if (MessageBox.Show($"Удалить заявку {selectedApplication.ApplicationId}?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string query = "DELETE FROM Applications WHERE ApplicationId=@ApplicationId";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@ApplicationId", selectedApplication.ApplicationId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    LoadApplications();
                }
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
    }


    // 🔹 Модель заявки
    public class ApplicationModel
    {
        public int ApplicationId { get; set; }
        public string UserName { get; set; }
        public string CategoryName { get; set; }
        public string Status { get; set; }
        public DateTime SubmissionDate { get; set; }
        public DateTime? ApprovalDate { get; set; }
    }
}
