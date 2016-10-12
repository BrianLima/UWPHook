using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;

namespace UWPHook
{
    public class AppEntry : INotifyPropertyChanged
    {
        private bool _isSelected;
        /// <summary>
        /// Gets or sets if the application is selected
        /// </summary>
        public bool Selected
        {
            get { return _isSelected; }
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
            }
        }


        private string _name;
        /// <summary>
        /// Gets or sets the name of the application
        /// </summary>
        public string Name
        {
            get { return _name; }
            set { _name = value; }
        }

        private string _aumid;
        /// <summary>
        /// Gets or sets the aumid of the application
        /// </summary>
        public string Aumid
        {
            get { return _aumid; }
            set { _aumid = value; }
        }

        private string _icon;

        public string Icon
        {
            get { return _icon; }
            set { _icon = value; }
        }

        public string widestSquareIcon()
        {
            string result = "";
            Size size = new Size(0, 0);
            List<string> images = new List<string>();

            //Get every file on the directory
            images.AddRange( Directory.GetFiles(_icon, "*.jpg", SearchOption.AllDirectories));
            images.AddRange(Directory.GetFiles(_icon, "*.png", SearchOption.AllDirectories));

            //Decide which is the largest
            foreach (string image in images)
            {
                Image i = Image.FromFile(image);

                if (i.Width == i.Height && (i.Size.Height > size.Height))
                {
                    size = i.Size;
                    result = image;
                }
            }

            return result;
        }


        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
