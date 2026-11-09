using System.Collections.ObjectModel;

namespace UWPHook
{
    public class AppEntryModel
    {
        public AppEntryModel()
        {
            this._entries = new ObservableCollection<AppEntry>();
        }

        private ObservableCollection<AppEntry> _entries;

        public ObservableCollection<AppEntry> Entries
        {
            get { return _entries; }
            set { _entries = value; }
        }
    }
}
