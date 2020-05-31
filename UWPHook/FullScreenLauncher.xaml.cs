using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// Interaction logic for FullScreenLauncher.xaml
    /// </summary>
    public partial class FullScreenLauncher : Window
    {
        public PaletteHelper pallet;
        public FullScreenLauncher()
        {
            InitializeComponent();
            textLaunch.Text = GetLauncherText();
            pallet = new PaletteHelper();

            BaseTheme darkTheme = BaseTheme.Dark;

            var theme = Theme.Create(darkTheme.GetBaseTheme(),
            SwatchHelper.Lookup[(MaterialDesignColor)PrimaryColor.DeepPurple],
            SwatchHelper.Lookup[(MaterialDesignColor)SecondaryColor.Lime]);

            pallet.SetTheme(theme);
        }

        private void Chip2_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=9YPV3FHEFRAUQ");
        }

        string GetLauncherText()
        {
            int n = DateTime.Now.Second;

            if (n >= 0 && n <= 10)
            {
                return "Hold on, i'm making your stream full screen!";
            }
            else if (n > 10 && n <= 20)
            {
                return "Waiting Steam in-home Streaming to catch up";
            }
            else if (n > 20 && n <= 30)
            {
                return "Starting Stream in a few seconds!";
            }
            else if (n > 30 && n <= 40)
            {
                return "Let's get this game started!";
            }
            else if (n > 40 && n <= 50)
            {
                return "Don't forget to check for updates at github.com/brianlima";
            }
            else
            {
                return "Good game, enjoy!";
            }
        }
    }
}
