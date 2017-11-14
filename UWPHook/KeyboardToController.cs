using System.Collections.Generic;
using ScpDriverInterface;


namespace UWPHook
{
    class KeyboardToController
    {
        private string _game;

        public string Game
        {
            get { return _game; }
            set { _game = value; }
        }

        private List<KeyToXboxButton> _listButtons;

        public List<KeyToXboxButton> ListButtons
        {
            get { return _listButtons; }
            set { _listButtons = value; }
        }
    }

    class KeyToXboxButton
    {
        private string _key;

        public string Key
        {
            get { return _key; }
            set { _key = value; }
        }

        private X360Buttons _X360Button;

        public X360Buttons x360Buttons
        {
            get { return _X360Button; }
            set { _X360Button = value; }
        }
    }
}
