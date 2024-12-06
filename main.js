const path = require('path');
const { app, BrowserWindow, ipcMain } = require('electron');

console.log('app.getAppPath():', app.getAppPath());
console.log('process.cwd():', process.cwd());
console.log('__dirname:', __dirname);

// Use __dirname to correctly resolve the path to the unpacked folder
const dotnet = require('node-api-dotnet');

console.log('dotnet:', dotnet);
// Resolve the VRCX.cjs path from the unpacked folder
const VRCXPath = path.join(__dirname, 'build/bin/AnyCPU/Debug/VRCX.cjs');
//console.log('VRCXPath:', VRCXPath);
//
require(VRCXPath);

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

function createWindow () {
  // Create the browser window.
  const mainWindow = new BrowserWindow({
    width: 1024,
    height: 768,
    icon: path.join(__dirname, 'VRCX.png'),
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
    }
  })

  // and load the index.html of the app.
  //mainWindow.loadFile('html/index.html')
  const indexPath = path.join(app.getAppPath(), 'build/html/index.html');
  mainWindow.loadFile(indexPath);

  // Open the DevTools.
  //mainWindow.webContents.openDevTools()
}

// This method will be called when Electron has finished
// initialization and is ready to create browser windows.
// Some APIs can only be used after this event occurs.
app.whenReady().then(() => {
  createWindow()

  app.on('activate', function () {
    // On macOS it's common to re-create a window in the app when the
    // dock icon is clicked and there are no other windows open.
    if (BrowserWindow.getAllWindows().length === 0) createWindow()
  })
})

// Quit when all windows are closed, except on macOS. There, it's common
// for applications and their menu bar to stay active until the user quits
// explicitly with Cmd + Q.
app.on('window-all-closed', function () {
  if (process.platform !== 'darwin') app.quit()
})

// In this file you can include the rest of your app's specific main process
// code. You can also put them in separate files and require them here.