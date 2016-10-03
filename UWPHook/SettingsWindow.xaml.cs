using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace UWPHook
{
    /// <summary>
    /// Interaction logic for SettingsWindow.xaml
    /// </summary>
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();

            Properties.Settings.Default.ChangeLanguage = true;

            foreach (var culture in CultureInfo.GetCultures(CultureTypes.AllCultures))
            {
                cultures_comboBox.Items.Add(culture.TextInfo.CultureName);
            }
        }

        private void ToggleButton_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.ChangeLanguage = (bool)language_toggle.IsChecked;
            Properties.Settings.Default.TargetLanguage = cultures_comboBox.SelectedItem.ToString();
            Properties.Settings.Default.Save();
        }
    }
}
