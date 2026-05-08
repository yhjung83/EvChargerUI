# FrontViewer

FrontViewer is a simple Windows Forms application for displaying a slideshow of images from a selected folder.

## Features

-   Select a folder to view images.
-   Lists all found images (.jpg, .jpeg, .png, .gif, .bmp).
-   Image slideshow in a borderless window.
-   Configurable slideshow interval.
-   Option to display the slideshow on a secondary monitor.
-   Settings are saved in a `Setting.ini` file.
-   `Dynamic Hot-Folder Updates`: Automatically updates the slideshow images when a special folder is detected.

## How to Build and Run

1.  **Build:**
    This project is a .NET Framework 4.0 project. You can build it using `msbuild` or Visual Studio.
    ```shell
    msbuild FrontViewer_4.0.csproj
    ```

2.  **Run:**
    After a successful build, the executable will be located in the `bin\Debug` or `bin\Release` folder.
    ```shell
    .\bin\Debug\FrontViewer_4.0.exe
    ```

## Configuration

The application uses a `Set\Setting.ini` file to store settings:

-   `SET_VIEWER_VISIBLE`: Shows or hides the main settings window on startup.
-   `SET_SUB_MONITER`: Toggles the slideshow display on the primary or a secondary monitor.
-   `SELECTED_PATH`: The path to the folder containing the images.
-   `SET_SWAP_TIMER`: The time in seconds between image transitions in the slideshow.

## Hot-Folder Image Updates
This feature allows you to update the slideshow images on the fly without restarting the application.

1.  In the same directory as `FrontViewer_4.0.exe`, create a new folder named `UpdateFrontFile`.
2.  Place all your new image files inside this `UpdateFrontFile` folder.
3.  The application will automatically detect the folder, update the images, and restart the slideshow.

**Important:** This process will delete all existing images in the target folder (defined by `SELECTED_PATH` in your settings) and replace them with the new ones. The `UpdateFrontFile` folder will be deleted after the update is complete.
