using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventHook;
using ScpDriverInterface;

namespace UWPHook
{
    public class EventsHook
    {
        private X360Controller controller;
        private ScpBus _scpBus;
        private byte[] _outputReport = new byte[8];
        KeyboardToController keyboardToController;

        public EventsHook(KeyboardToController _keyboardToController)
        {
            keyboardToController = _keyboardToController;
        }

        public void StartHooking()
        {
            KeyboardWatcher.Start();
            MouseWatcher.Start();

            MouseWatcher.OnMouseInput += MouseWatcher_OnMouseInput;
            KeyboardWatcher.OnKeyInput += KeyboardWatcher_OnKeyInput;
            controller = new X360Controller();

            _outputReport = new byte[8];
            try
            {
                _scpBus = new ScpBus();

                _scpBus.PlugIn((int)1);
            }
            catch (Exception)
            {

                throw;
            }
        }

        private void MouseWatcher_OnMouseInput(object sender, MouseEventArgs e)
        {
        }

        private void KeyboardWatcher_OnKeyInput(object sender, KeyInputEventArgs e)
        {
            KeyToXboxButton button = (KeyToXboxButton)keyboardToController.ListButtons.Select(x => x.Key == e.KeyData.Keyname);

            controller.Buttons ^= button.x360Buttons;
            _scpBus.Report((int)1, controller.GetReport(), _outputReport);
        }

        internal void StopHooking()
        {
            KeyboardWatcher.Stop();
            MouseWatcher.Stop();
        }
    }
}
