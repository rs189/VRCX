const fs = require('fs');
const path = require('path');

function patchFile(filePath) {
  if (!fs.existsSync(filePath)) {
    console.error(`Error: ${filePath} does not exist.`);
    return false;
  }

  let fileContent = fs.readFileSync(filePath, 'utf8');
  
  // Log the current content for debugging
  //console.log('Current file content:');
  //console.log(fileContent);

  // More flexible regex that allows for different quote types and whitespace
  const regex = /const\s+managedHostPath\s*=\s*__dirname\s*\+\s*`\/\$\{targetFramework\}\/\$\{assemblyName\}\.DotNetHost\.dll`/;
  
  const newContent = fileContent.replace(
    regex,
    `let managedHostPath = __dirname + \`/\${targetFramework}/\${assemblyName}.DotNetHost.dll\`;
managedHostPath = managedHostPath.indexOf('app.asar.unpacked') < 0 ?
  managedHostPath.replace('app.asar', 'app.asar.unpacked') : managedHostPath;`
  );

  if (fileContent !== newContent) {
    fs.writeFileSync(filePath, newContent, 'utf8');
    console.log(`Patched: ${filePath}`);
    
    // Log the new content for verification
    //console.log('New file content:');
    //console.log(newContent);
    
    return true;
  }
  
  console.log(`No changes needed for: ${filePath}`);
  return false;
}

// Paths to patch
const postBuildPath = path.join(__dirname, '/build/linux-unpacked/resources/app.asar.unpacked/node_modules/node-api-dotnet/init.js');
console.log('Patching post-build init.js...');
patchFile(postBuildPath);