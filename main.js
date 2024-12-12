require('hazardous');
const path = require('path');
const { app, BrowserWindow, ipcMain, Menu, Tray } = require('electron');

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

function createWindow() {
    const mainWindow = new BrowserWindow({
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

app.whenReady().then(() => {
    createWindow();

    createTray();

    app.on('activate', function () {
        if (BrowserWindow.getAllWindows().length === 0) createWindow();
    });
});

app.on('window-all-closed', function () {
    if (process.platform !== 'darwin') app.quit();
});
