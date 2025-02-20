using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.VisualBasic;

namespace TicketLightAdmin.Pages
{
    public partial class BenefitCategoriesPage : Page
    {
        private string connectionString = "Server=TEMHANLAPTOP\\TDG2022;Database=TicketLight;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True;";
        private List<BenefitCategory> categories = new List<BenefitCategory>();

        public BenefitCategoriesPage()
        {
            InitializeComponent();
            this.Loaded += LoadCategories_Loaded; // Привязываем событие Loaded
        }

        private void LoadCategories_Loaded(object sender, RoutedEventArgs e)
        {
            LoadCategories();
        }

        // 🔹 Загрузка категорий из БД
        private void LoadCategories()
        {
            categories.Clear();

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
                            CategoryName = reader.GetString(1),
                            Description = reader.GetString(2)
                        });
                    }
                }
            }

            CategoriesDataGrid.ItemsSource = null;
            CategoriesDataGrid.ItemsSource = categories;
        }

        // 🔹 Добавление категории
        private void AddCategory_Click(object sender, RoutedEventArgs e)
        {
            string newName = Interaction.InputBox("Введите название категории:", "Добавление", "");
            string newDescription = Interaction.InputBox("Введите описание категории:", "Добавление", "");

            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Название категории не может быть пустым!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            using (SqlConnection conn = new SqlConnection(connectionString))
            {
                conn.Open();
                string query = "INSERT INTO BenefitCategories (CategoryName, Description) VALUES (@CategoryName, @Description)";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@CategoryName", newName);
                    cmd.Parameters.AddWithValue("@Description", newDescription);
                    cmd.ExecuteNonQuery();
                }
            }

            LoadCategories();
        }

        // 🔹 Редактирование категории
        private void EditCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesDataGrid.SelectedItem is BenefitCategory selectedCategory)
            {
                string newName = Interaction.InputBox("Введите новое название:", "Редактирование", selectedCategory.CategoryName);
                string newDescription = Interaction.InputBox("Введите новое описание:", "Редактирование", selectedCategory.Description);

                if (string.IsNullOrWhiteSpace(newName))
                {
                    MessageBox.Show("Название категории не может быть пустым!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                using (SqlConnection conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    string query = "UPDATE BenefitCategories SET CategoryName=@CategoryName, Description=@Description WHERE CategoryId=@CategoryId";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@CategoryName", newName);
                        cmd.Parameters.AddWithValue("@Description", newDescription);
                        cmd.Parameters.AddWithValue("@CategoryId", selectedCategory.CategoryId);
                        cmd.ExecuteNonQuery();
                    }
                }

                LoadCategories();
            }
        }

        // 🔹 Удаление категории
        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoriesDataGrid.SelectedItem is BenefitCategory selectedCategory)
            {
                if (MessageBox.Show($"Удалить категорию {selectedCategory.CategoryName}?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    using (SqlConnection conn = new SqlConnection(connectionString))
                    {
                        conn.Open();
                        string query = "DELETE FROM BenefitCategories WHERE CategoryId=@CategoryId";

                        using (SqlCommand cmd = new SqlCommand(query, conn))
                        {
                            cmd.Parameters.AddWithValue("@CategoryId", selectedCategory.CategoryId);
                            cmd.ExecuteNonQuery();
                        }
                    }

                    LoadCategories();
                }
            }
        }
    }

    // 🔹 Модель категории льгот
    public class BenefitCategory
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public string Description { get; set; }
    }
}
