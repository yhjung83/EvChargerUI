# WinWatchDogApp

WinWatchDogApp is a simple Windows desktop utility that monitors specified applications and automatically restarts them if they stop running. This ensures your critical processes remain active.

## Features

*   **Process Monitoring:** Monitor up to 2 applications simultaneously.
*   **Automatic Restart:** If a monitored application closes, WinWatchDogApp will restart it after a configurable delay.
*   **Start with Windows:** Automatically launch the application when you log in to Windows.
*   **Configurable Countdown:** Set a countdown timer (in seconds) for each process to define the delay before a restart attempt.
*   **Enable/Disable:** Easily toggle monitoring for each process on or off.
*   **Persistent Configuration:** Settings are saved to a `WatchDog.ini` file and loaded automatically on startup.

## How to Use

1.  Launch the application (`WinWatchDogApp.exe`).
2.  For each of the two process slots:
    *   Click the **folder icon** to open a file dialog.
    *   Select the executable file (`.exe`) of the application you want to monitor.
    *   Set the **countdown timer** (in seconds) in the textbox. This is the time the watchdog will wait before restarting the process after it's found to be closed.
    *   Use the **toggle switch** to enable or disable monitoring for that process.
3.  To have the application run on login, check the **Start with Windows** checkbox at the bottom.
4.  Click the **Save** button to store your configuration.

## Configuration

Your settings are stored in a `WatchDog.ini` file located in the same directory as the executable. The file has the following format:

```ini
[Process1]
Path=C:\path\to\your\process1.exe
Timer=60
Enabled=true

[Process2]
Path=
Timer=10
Enabled=false

[Setting]
AutoStart=true
```

*   `Path`: The full path to the executable.
*   `Timer`: The restart delay in seconds.
*   `Enabled`: `true` if monitoring is active, `false` otherwise.
*   `AutoStart`: `true` if the application should start with Windows, `false` otherwise.

You can edit this file manually, but the application will overwrite it when you click "Save".

## Building from Source

To build the application from the source code, you will need:

*   **Visual Studio** (2010 or later)
*   **.NET Framework 4.0**

Open the `WinWatchDogApp.sln` file in Visual Studio and build the project.

## Contributing

This is a very simple project. A key area for improvement is making the number of monitored processes dynamic instead of being hardcoded to 2. Pull requests are welcome.

## License

This project is licensed under the MIT License.