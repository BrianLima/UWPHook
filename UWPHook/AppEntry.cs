using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Reflection;
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

        private string _executable;
        /// <summary>
        /// Gets or sets the executable of the application
        /// </summary>
        public string Executable
        {
            get { return _executable; }
            set { _executable = value; }
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

        /// <summary>
        /// Gets or sets the icon for the app
        /// </summary>
        private string _icon;

        public string Icon
        {
            get { return _icon; }
            set { _icon = value; }
        }


        /// <summary>
        /// Sets the path where icons for the app is 
        /// </summary>
        private string _icon_path;

        public string IconPath
        {
            get { return _icon_path; }
            set { _icon_path = value; }
        }

        public string widestSquareIcon()
        {
            string result = "";
            Size size = new Size(0, 0);
            List<string> images = new List<string>();


            try
            {
                //Get every png in this directory, Steam only allows for .png's
                images.AddRange(Directory.GetFiles(_icon_path, "*.png"));
            }
            catch (DirectoryNotFoundException)
            {
                // Issue #56
                return string.Empty;
            }

            //Decide which is the largest
            foreach (string image in images)
            {
                Image icon = null;

                //Try to load the image, if it's a invalid file, skip it
                try
                {
                    icon = Image.FromFile(image);
                }
                catch (System.Exception)
                {

                }

                if (icon != null)
                {
                    //UWP apps usually store live tile images inside the same directory
                    //Let's check if the image is square for use as icon on Steam and pick the largest one
                    if (icon.Width == icon.Height && (icon.Size.Height > size.Height))
                    {
                        size = icon.Size;
                        result = image;
                    }
                }
            }

            return result;
        }

        public string isKnownApp()
        {
            if(AppManager.IsKnownApp(_aumid, out string name))
            {
                return name;
            }

            return "Name not found, double click here to edit";
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return $"{Name} ({Aumid})";
        }
    }
}
