using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using UWPHook.Properties;

namespace UWPHook
{
    public class GameModel
    {
        public GameModel()
        {
            games = new ObservableCollection<Game>();
            var y = JsonConvert.DeserializeObject<GameModel>("games.json");
        }

        private ObservableCollection<Game> _games;

        public ObservableCollection<Game> games
        {
            get { return _games; }
            set { _games = value; }
        }

        public void Add(Game game)
        {
            this.games.Add(game);
        }

        public void Store()
        {
            JsonSerializer serializer = new JsonSerializer();
            using (StreamWriter sw = new StreamWriter(@"games.json")) {
                using (JsonWriter writer = new JsonTextWriter(sw)){
                    serializer.Serialize(writer, _games);
                }
            }
        }
    }

    public class Game : INotifyPropertyChanged
    {
        private string _game_alias;

        public string game_alias
        {
            get { return _game_alias; }
            set { _game_alias = value; }
        }

        private string _game_path;

        public string game_path
        {
            get { return _game_path; }
            set { _game_path = value; }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string Obj)
        {
            if (PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(Obj));
            }
        }
    }
}
