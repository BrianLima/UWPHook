namespace VDFParser.Models {

    /// <summary>
    /// Represents a VDF entry
    /// Reference: https://developer.valvesoftware.com/wiki/Add_Non-Steam_Game
    /// </summary>
    public class VDFEntry {

        /// <summary>
        /// Gets or sets the index.
        /// </summary>
        /// <value>The index.</value>
        public int Index { get; set; }

        /// <summary>
        /// Gets or sets the appid of the app.
        /// </summary>
        /// <value>The id of the app.</value>
        [VDFField("appid")]
        public int appid { get; set; }

        /// <summary>
        /// Gets or sets the name of the app.
        /// </summary>
        /// <value>The name of the app.</value>
        [VDFField("AppName")]
        public string AppName { get; set; }

        /// <summary>
        /// Gets or sets the app executable path.
        /// Must use the following format:
        /// "/path/to/binary"
        /// Extra arguments should be provided in LaunchOptions,
        /// but may also be provided within this string using the format:
        /// "/path/to/binary" -arg1 "argument with spaces"
        /// </summary>
        /// <value>The executable path.</value>
        [VDFField("Exe")]
        public string Exe { get; set; }

        /// <summary>
        /// Gets or sets the application start directory
        /// </summary>
        /// <value>The start directory</value>
        [VDFField("StartDir")]
        public string StartDir { get; set; }

        /// <summary>
        /// Gets or sets the application icon
        /// </summary>
        /// <value>The application icon</value>
        [VDFField("icon")]
        public string Icon { get; set; }

        /// <summary>
        /// Gets or sets the application shortcut path.
        /// </summary>
        /// <value>The application shortcut path.</value>
        [VDFField("ShortcutPath")]
        public string ShortcutPath { get; set; }

        /// <summary>
        /// Gets or sets the application launch options.
        /// </summary>
        /// <value>The application launch options.</value>
        [VDFField("LaunchOptions")]
        public string LaunchOptions { get; set; }

        /// <summary>
        /// Gets or sets whether the application is hidden. 
        /// Use 1 for true, or 0 otherwise.
        /// </summary>
        /// <value>Whether the application is hidden.</value>
        [VDFField("IsHidden", Type = VDFFieldType.Integer)]
        public int IsHidden { get; set; }

        /// <summary>
        /// Gets or sets whether desktop configuration is allowed.
        /// Use 1 for true, or 0 otherwise.
        /// </summary>
        /// <value>Whether the desktop configuration is allowed</value>
        [VDFField("AllowDesktopConfig", Type = VDFFieldType.Integer)]
        public int AllowDesktopConfig { get; set; }

        /// <summary>
        /// Gets or sets whether Steam overlay is allowed.
        /// Use 1 for true, or 0 otherwise.
        /// </summary>
        /// <value>Whether the Steam overlay is allowed</value>
        [VDFField("AllowOverlay", Type = VDFFieldType.Integer)]
        public int AllowOverlay { get; set; }

        /// <summary>
        /// Gets or sets whether this is an OpenVR application.
        /// Use 1 for true, or 0 otherwise.
        /// </summary>
        /// <value>Whether this is an OpenVR application</value>
        [VDFField("openvr", Type = VDFFieldType.Integer)]
        public int OpenVR { get; set; }

        /// <summary>
        /// Gets or sets whether this is an Devkit application.
        /// Use 1 for true, or 0 otherwise.
        /// </summary>
        /// <value>Whether this is an Devkit application</value>
        [VDFField("Devkit", Type = VDFFieldType.Integer)]
        public int Devkit { get; set; }

        /// <summary>
        /// Gets or sets the Devkit Game ID for this application.
        /// </summary>
        /// <value>Devkit Game ID for this application</value>
        [VDFField("DevkitGameID")]
        public string DevkitGameID { get; set; }

        /// <summary>
        /// Gets or sets the last play time for this application.
        /// Use a UNIX timestamp, or 0 for never played.
        /// </summary>
        /// <value>Last Play Time as UNIX timestamp</value>
        [VDFField("LastPlayTime", Type = VDFFieldType.Integer)]
        public int LastPlayTime { get; set; }

        /// <summary>
        /// Gets or sets a list of tags for the application
        /// </summary>
        /// <value>Tags list for the application</value>
        [VDFField("tags", Type = VDFFieldType.IndexedArray)]
        public string[] Tags { get; set; }

        /// <summary>
        /// Returns a <see cref="T:System.String"/> that represents the current <see cref="T:VDFParser.Models.VDFEntry"/>.
        /// </summary>
        /// <returns>A <see cref="T:System.String"/> that represents the current <see cref="T:VDFParser.Models.VDFEntry"/>.</returns>
        public override string ToString() {
            return string.Format("[VDFEntry: AppName={0}, Exe={1}, StartDir={2}, Icon={3}, ShortcutPath={4}, LaunchOptions={5}, IsHidden={6}, AllowDesktopConfig={7}, AllowOverlay={8}, OpenVR={9}, Devkit={10}, DevkitGameID={11}, LastPlayTime={12}, Tags={13}]", AppName, Exe, StartDir, Icon, ShortcutPath, LaunchOptions, IsHidden, AllowDesktopConfig, AllowOverlay, OpenVR, Devkit, DevkitGameID, LastPlayTime, Tags == null ? "(null)" : string.Join(", ", Tags));
        }
    }
}
