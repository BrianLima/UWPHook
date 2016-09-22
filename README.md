# UWPHook

![](http://imgur.com/gWwR02D.png)

Small project to link UWP games to Steam

If you want to add Windows Store Games to Steam to show up on your Status you need to do a bit of a warkaround because Steam can't see UWP apps, this program aims to simplify a little bit the process where it is possible by automating the scripting and launching.

# To add UWP games to Steam #

Download UWPHook, it's files and store them somewhere on your PC.

![](http://i.imgur.com/sH61SYT.png)

Create a shortcut to UWPHook on the same directory where you installed it, by right clicking it and choosing "Create Shortcut" and name it something simple that resembles the game you want to add to Steam.

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

Click Play and enjoy your UWP game showing up on your Steam Status, UWPHook will run on the background checking every 5 seconds if your game is running and close it self automatically if the game is not running.

----------

# Adding or Removing Games from UWPHook #

If you want to play a game that is not included by default on UWPHook, then follow these steps:

Open UWPHook, type the alias of the game in the alias textbox

## Finding the game URL ##

Go to "C:\Users\<user>\AppData\Local\Packages"

Find the App you want to add, copy the whole folder name and add "!App" to the end on UWPHook, should look like this:

![](http://imgur.com/NMx9IAR.png)

![](http://imgur.com/2RlKi1X.png)

Repeat previous steps to add the game to Steam.

If you can't find the app, there's a easier way to determine its folder name:

Press "Windows+R" and type "shell:appsfolder", you will be taken to a folder containing every metro app and other things on your PC

![](http://imgur.com/W1kH0R4.png)

Find the game you want, right click it and create shortcut, Windows will place a shortcut on your desktop.

![](http://imgur.com/Z9p80Hy.png)

Right click the shortcut, on targettype will be the app's folder name, making it easier to find the game on Packages folder.

![](http://imgur.com/HU3I2NU.png)

To remove a game form the list, double click the desired game

# Troubleshooting #

- **I added the game's url to UWPHook but it won't start whatsoever**
- Some games use another string at the end besides "!App" like Forza Motorsport 6: Apex for example, if you can't figure it out, someone at our subreddit might help you.

- **Steam Big Picture Mode isn't working!**
- Unfortunately, it's a Steam limitation and no overlays work yet with UWP games. 

- **My question isn't listed here!**
- Drop by our subreddit and ask a question over there, maybe someone will help you, i surely will as soon as i can
 **[https://www.reddit.com/r/uwphook](https://www.reddit.com/r/uwphook)**



**#This software is licensed under the MIT license #**
----------
The MIT License (MIT) Copyright © 2014 
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
