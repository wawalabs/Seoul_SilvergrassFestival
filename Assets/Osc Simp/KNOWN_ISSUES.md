#Known Issues


### Broadcasting from iOS not working.
Tested 230515 with Unity 2022.2.2 on iOS 16.4.1.
While receiving broadcast messages on iOS does work, sending broadcase messeges does not.


### OscIn mappings targeting public fields executes slow when the IL2CPP compiler is enabled.
IL2CPP does not support System.Reflection.Eimt and so we are forced to use standard reflection which is slow. This is not a problem when targeting properties or when the Mono compiler is enabled.


### Interruptions on MacOS builds when window is background.
On MacOS 10.14.2 builds (and possibly higher versions) networking is blocked when running in Window mode and hidden behind other windows, even though Application.runInBackground is enabled. This causes regular interruptions in messages send by OSC simpl.
https://forum.unity.com/threads/macos-build-has-update-loop-hiccups-in-window-mode.627781/