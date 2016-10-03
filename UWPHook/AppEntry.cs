using System.ComponentModel;
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

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
