require('hazardous');
const path = require('path');
const { BrowserWindow, ipcMain, app, globalShortcut, Tray, Menu, dialog } = require('electron');
const fs = require('fs');
const https = require('https');

const dotnet = require('node-api-dotnet/net8.0');
require(path.join(__dirname, 'build/bin/Debug/VRCX.cjs'));

const InteropApi = require('./InteropApi');
const interopApi = new InteropApi();

interopApi.getDotNetObject('DynamicProgram').PreInit();
interopApi.getDotNetObject('VRCXStorage').Load();
interopApi.getDotNetObject('DynamicProgram').Init();
interopApi.getDotNetObject('SQLiteLegacy').Init();
interopApi.getDotNetObject('AppApi').Init();
interopApi.getDotNetObject('Discord').Init();
interopApi.getDotNetObject('WebApi').Init();
interopApi.getDotNetObject('LogWatcher').Init();
interopApi.getDotNetObject('AutoAppLaunchManager').Init();

ipcMain.handle('callDotNetMethod', (event, className, methodName, args) => {
    return interopApi.callMethod(className, methodName, args);
});

var mainWindow = undefined;

ipcMain.handle('dialog:openFile', async () => {
    const result = await dialog.showOpenDialog(mainWindow, {
        properties: ['openFile'],
        filters: [{ name: 'Images', extensions: ['png'] }]
    });

    if (!result.canceled && result.filePaths.length > 0) {
        return result.filePaths[0];
    }
    return null;
});

function createWindow() {
    app.commandLine.appendSwitch('enable-speech-dispatcher')

    mainWindow = new BrowserWindow({
        width: 1024,
        height: 768,
        icon: path.join(__dirname, 'VRCX.png'),
        webPreferences: {
            preload: path.join(__dirname, 'preload.js')
        }
    });

    const indexPath = path.join(app.getAppPath(), 'build/html/index.html');
    mainWindow.loadFile(indexPath);

    // Open the DevTools.
    //mainWindow.webContents.openDevTools()

	globalShortcut.register('Control+=', () => {
		mainWindow.webContents.setZoomLevel(mainWindow.webContents.getZoomLevel() + 1)
	})
}

function createTray() {
    const tray = new Tray(path.join(__dirname, 'images/tray.png'));
    const contextMenu = Menu.buildFromTemplate([
        {
            label: 'Open',
            type: 'normal',
            click: function () {
                BrowserWindow.getAllWindows().forEach(function (win) {
                    win.show();
                });
            }
        },
        {
            label: 'DevTools',
            type: 'normal',
            click: function () {
                BrowserWindow.getAllWindows().forEach(function (win) {
                    win.webContents.openDevTools();
                });
            }
        },
        {
            label: 'Quit VRCX',
            type: 'normal',
            click: function () {
                app.quit();
            }
        }
    ]);
    tray.setToolTip('VRCX');
    tray.setContextMenu(contextMenu);
}

async function installVRCX() {
    let appImagePath = process.env.APPIMAGE;
    if (!appImagePath) {
        console.error('AppImage path is not available!');
        return;
    }

    let currentName = path.basename(appImagePath);
    let newName = currentName.replace(/VRCX_\d{8}/, 'VRCX');
    if (currentName !== newName) {
        const newPath = path.join(path.dirname(appImagePath), newName);
        try {
            fs.renameSync(appImagePath, newPath);
            console.log('AppImage renamed to:', newPath);
            appImagePath = newPath;
        } catch (err) {
            console.error('Error renaming AppImage:', err);
            dialog.showErrorBox('Error', 'Failed to rename AppImage.');
            return;
        }
    }

    if (appImagePath.startsWith(path.join(app.getPath('home'), 'Applications'))) {
        console.log('VRCX is already installed.');
        return;
    }

    const targetPath = path.join(app.getPath('home'), 'Applications');
    console.log('AppImage Path:', appImagePath);
    console.log('Target Path:', targetPath);

    // Create target directory if it doesn't exist
    if (!fs.existsSync(targetPath)) {
        fs.mkdirSync(targetPath);
    }

    // Extract the filename from the AppImage path
    const appImageName = path.basename(appImagePath);
    const targetAppImagePath = path.join(targetPath, appImageName);

    // Move the AppImage to the target directory
    try {
        fs.renameSync(appImagePath, targetAppImagePath);
        console.log('AppImage moved to:', targetAppImagePath);
    } catch (err) {
        console.error('Error moving AppImage:', err);
        dialog.showErrorBox('Error', 'Failed to move AppImage.');
        return;
    }

	// Download the icon and save it to the target directory
    const iconUrl = 'https://raw.githubusercontent.com/vrcx-team/VRCX/master/VRCX.png';
    const targetIconPath = path.join(targetPath, 'VRCX.png');
    downloadIcon(iconUrl, targetIconPath)
        .then(() => {
            console.log('Icon downloaded and saved to:', targetIconPath);

    		// Create a .desktop file in ~/.local/share/applications/
    		const desktopFile = `[Desktop Entry]
Name=VRCX
Exec=${targetAppImagePath}
Icon=${targetIconPath}
Type=Application
Categories=Network;InstantMessaging;Game;
Terminal=false
StartupWMClass=VRCX
`;

            const desktopFilePath = path.join(app.getPath('home'), '.local/share/applications/VRCX.desktop');
            try {
                fs.writeFileSync(desktopFilePath, desktopFile);
                console.log('Desktop file created at:', desktopFilePath);
            } catch (err) {
                console.error('Error creating desktop file:', err);
                dialog.showErrorBox('Error', 'Failed to create desktop entry.');
                return;
            }
        })
        .catch((err) => {
            console.error('Error downloading icon:', err);
            dialog.showErrorBox('Error', 'Failed to download the icon.');
        });
}

// Function to download the icon and save it to a specific path
function downloadIcon(url, targetPath) {
    return new Promise((resolve, reject) => {
        const file = fs.createWriteStream(targetPath);
        https.get(url, (response) => {
            if (response.statusCode !== 200) {
                reject(new Error(`Failed to download icon, status code: ${response.statusCode}`));
                return;
            }
            response.pipe(file);
            file.on('finish', () => {
                file.close(resolve);
            });
        }).on('error', (err) => {
            fs.unlink(targetPath, () => reject(err)); // Delete the file if error occurs
        });
    });
}

app.whenReady().then(() => {
    createWindow();

    createTray();

	installVRCX();

    app.on('activate', function () {
        if (BrowserWindow.getAllWindows().length === 0) createWindow();
    });
});

app.on('window-all-closed', function () {
    if (process.platform !== 'darwin') app.quit();
});
