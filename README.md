# UWPHook
Small project to link UWP games to Steam

If you want to add Windows Store Games to Steam to show up on your Status you need to do a bit of a warkaround because Steam cand see UWP apps, this program aims to simplify a little bit the process where it is possible by automating the scripting and launching.

# To add UWP games to Steam #

Download UWPHook, it's files and store them somewhere on your PC.

![](http://i.imgur.com/sH61SYT.png)

Create a shortcut to UWPHook by right clicking it and choosing "Create Shortcut" and name it something simple that resembles the game you want to add to Steam.

![](http://i.imgur.com/mPWKhkt.png) 

![](http://i.imgur.com/FnXYTQH.png)

Go to Steam and click "Add non-Steam Game to Steam

![](http://i.imgur.com/QyJIdnr.png)

Click Browse, find where you extracted UWPHook.exe and select it.

![](http://i.imgur.com/drbwhyK.png)

Click "Add Selected Programs"

Find UWPHook at your Library, right click it, change the name from UWPHook to the name of the game you want, change target to the name of the shortcut you created

![](http://imgur.com/QmsTPpA.png)

![](http://imgur.com/03aEp3Z.png)

Click "Set Launch Options" and write the alias of the game you want to play, you can find it in the column "Alias" on UWPHook

![](http://imgur.com/FtGqaiR.png)

Click Play and enjoy your UWP game showing up on your Steam Status, as long as UWPHook is running

----------

# Adding or Removing Games from UWPHook #

If you want to play a game that is not included by default on UWPHook, then follow these steps:

Open UWPHook, type the alias of the game in the alias textbox

## Finding the game URL ##

Go to "C:\Users\<user>\AppData\Local\Packages"

Find the App you want to add, copy the whole folder name and add "!App" to the end on UWPHook, should look like this:

Repeat previous steps to add the game to Steam.

If you can't find the app, there's a easier way to determine its folder name:

Press "Windows+R" and type "shell:appsfolder", you will be taken to a folder containing every metro app and other things on your PC

Find the game you want, right click it and create shortcut, Windows will place a shortcut on your desktop.

Right click the shortcut, on targettype will be the app's folder name, making it easier to find the game on Packages folder.

# Troubleshooting #

- **I added the game's url to UWPHook but it won't start whatsoever**
- Some games use another string at the end besides "!App" like Forza Motorsport 6: Apex for example, if you can't figure it out, someone at our subreddit might help you.

- **My question isn't listed here!**
- Drop by our subreddit and ask a question over there, maybe someone will help you, i surely will as soon as i can
 **[https://www.reddit.com/r/uwphook](https://www.reddit.com/r/uwphook)**