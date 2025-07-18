using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;

namespace TicketLightAdmin.Pages
{
    public partial class BenefitCategoriesPage : Page
    {
        private string connectionString = "Server=TEMHANLAPTOP\\TDG2022;Database=TicketLight2;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        private List<BenefitCategory> categories = new List<BenefitCategory>();

        public BenefitCategoriesPage()
        {
            InitializeComponent();
            this.Loaded += BenefitCategoriesPage_Loaded;
        }

        private void BenefitCategoriesPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCategories();
        }

        private void LoadCategories()
        {
            categories.Clear();
            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "SELECT CategoryId, CategoryName, Description FROM BenefitCategories";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            categories.Add(new BenefitCategory
                            {
                                CategoryId = reader.GetInt32(0),
                                CategoryName = reader.IsDBNull(1) ? null : reader.GetString(1),
                                Description = reader.IsDBNull(2) ? null : reader.GetString(2)
                            });
                        }
                    }
                }
                CategoriesDataGrid.ItemsSource = null;
                CategoriesDataGrid.ItemsSource = categories;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки категорий: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CategoryNameTextBox.Text) || string.IsNullOrEmpty(DescriptionTextBox.Text))
            {
                MessageBox.Show("Заполните все поля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "INSERT INTO BenefitCategories (CategoryName, Description) VALUES (@CategoryName, @Description)";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CategoryName", CategoryNameTextBox.Text);
                        cmd.Parameters.AddWithValue("@Description", DescriptionTextBox.Text);
                        cmd.ExecuteNonQuery();
                    }
                }
                LoadCategories();
                CategoryNameTextBox.Text = string.Empty;
                DescriptionTextBox.Text = string.Empty;
                MessageBox.Show("Категория добавлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка добавления категории: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EditCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesDataGrid.SelectedItem is BenefitCategory selectedCategory)
            {
                string newName = Interaction.InputBox("Введите новое название:", "Редактирование", selectedCategory.CategoryName);
                string newDescription = Interaction.InputBox("Введите новое описание:", "Редактирование", selectedCategory.Description);

                try
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string query = "UPDATE BenefitCategories SET CategoryName=@CategoryName, Description=@Description WHERE CategoryId=@CategoryId";
                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@CategoryName", string.IsNullOrWhiteSpace(newName) ? selectedCategory.CategoryName : newName);
                            cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(newDescription) ? selectedCategory.Description : newDescription);
                            cmd.Parameters.AddWithValue("@CategoryId", selectedCategory.CategoryId);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    LoadCategories();
                    MessageBox.Show("Категория обновлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка редактирования категории: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesDataGrid.SelectedItem is BenefitCategory selectedCategory)
            {
                if (MessageBox.Show($"Удалить категорию: {selectedCategory.CategoryName}?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    try
                    {
                        using (SqlConnection conn = new SqlConnection(connectionString))
                        {
                            conn.Open();
                            string checkQuery = "SELECT COUNT(*) FROM Applications WHERE CategoryId = @CategoryId";
                            using (SqlCommand checkCmd = new SqlCommand(checkQuery, conn))
                            {
                                checkCmd.Parameters.AddWithValue("@CategoryId", selectedCategory.CategoryId);
                                int count = (int)checkCmd.ExecuteScalar();
                                if (count > 0)
                                {
                                    MessageBox.Show("Нельзя удалить категорию, так как она используется в заявках!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                    return;
                                }
                            }

                            string deleteQuery = "DELETE FROM BenefitCategories WHERE CategoryId = @CategoryId";
                            using (SqlCommand cmd = new SqlCommand(deleteQuery, conn))
                            {
                                cmd.Parameters.AddWithValue("@CategoryId", selectedCategory.CategoryId);
                                cmd.ExecuteNonQuery();
                            }
                        }
                        LoadCategories();
                        MessageBox.Show("Категория удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка удаления категории: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }

    public class BenefitCategory
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string Description { get; set; }
    }
}