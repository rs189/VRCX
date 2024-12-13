using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace VRCX
{
    public partial class AppApi
    {
        [DllImport("advapi32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern uint RegSetValueEx(
            UIntPtr hKey,
            [MarshalAs(UnmanagedType.LPStr)] string lpValueName,
            int Reserved,
            RegistryValueKind dwType,
            byte[] lpData,
            int cbData);

        [DllImport("advapi32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        public static extern int RegOpenKeyEx(
            UIntPtr hKey,
            string subKey,
            int ulOptions,
            int samDesired,
            out UIntPtr hkResult);

        [DllImport("advapi32.dll")]
        public static extern int RegCloseKey(UIntPtr hKey);

        public string AddHashToKeyName(string key)
        {
            // https://discussions.unity.com/t/playerprefs-changing-the-name-of-keys/30332/4
            // VRC_GROUP_ORDER_usr_032383a7-748c-4fb2-94e4-bcb928e5de6b_h2810492971
            uint hash = 5381;
            foreach (var c in key)
                hash = (hash * 33) ^ c;
            return key + "_h" + hash;
        }
#if LINUX
        private int FindMatchingBracket(string content, int openBracketIndex)
        {
            int depth = 0;
            for (int i = openBracketIndex; i < content.Length; i++)
            {
                if (content[i] == '{')
                    depth++;
                else if (content[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }
            return -1;
        }

        private Dictionary<string, string> ExtractCompatToolMapping(string vdfContent)
        {
            var compatToolMapping = new Dictionary<string, string>();
            const string sectionHeader = "\"CompatToolMapping\"";
            int sectionStart = vdfContent.IndexOf(sectionHeader);

            if (sectionStart == -1)
            {
                Console.WriteLine("CompatToolMapping not found");
                return compatToolMapping;
            }

            int blockStart = vdfContent.IndexOf("{", sectionStart) + 1;
            int blockEnd = FindMatchingBracket(vdfContent, blockStart - 1);

            if (blockStart == -1 || blockEnd == -1)
            {
                Console.WriteLine("CompatToolMapping block not found");
                return compatToolMapping;
            }

            string blockContent = vdfContent.Substring(blockStart, blockEnd - blockStart);

            var keyValuePattern = new Regex("\"(\\d+)\"\\s*\\{[^}]*\"name\"\\s*\"([^\"]+)\"", 
                RegexOptions.Multiline);

            var matches = keyValuePattern.Matches(blockContent);
            foreach (Match match in matches)
            {
                string key = match.Groups[1].Value;
                string name = match.Groups[2].Value;

                if (key != "0")
                {
                    compatToolMapping[key] = name;
                }
            }

            return compatToolMapping;
        }

        public string GetSteamVdfCompatTool()
        {
            string steamPath = LogWatcher.GetSteamPath();
            string configVdfPath = Path.Combine(steamPath, "config", "config.vdf");
            if (!File.Exists(configVdfPath))
            {
                Console.WriteLine("config.vdf not found");
                return null;
            }

            string vdfContent = File.ReadAllText(configVdfPath);
            var compatToolMapping = ExtractCompatToolMapping(vdfContent);

            if (compatToolMapping.TryGetValue("438100", out string name))
            {
                return name;
            }

            return null;
        }

        private string ParseWineRegOutput(string output, string keyName)
        {
            if (string.IsNullOrEmpty(output))
                return null;
            
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => 
                    !string.IsNullOrWhiteSpace(line) && 
                    !line.Contains("fixme:") && 
                    !line.Contains("wine:"))
                .ToArray();
        
            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => p.Trim())
                                .ToArray();
                if (parts.Length >= 3 && parts[0].Contains(keyName))
                {
                    var valueType = parts[parts.Length - 2];
                    var value = parts[parts.Length - 1];

                    switch (valueType)
                    {
                        case "REG_BINARY":
                            try 
                            {
                                // Treat the value as a plain hex string and decode it to ASCII
                                var hexValues = Enumerable.Range(0, value.Length / 2)
                                    .Select(i => value.Substring(i * 2, 2)) // Break string into chunks of 2
                                    .Select(hex => Convert.ToByte(hex, 16)) // Convert each chunk to a byte
                                    .ToArray();

                                return Encoding.ASCII.GetString(hexValues).TrimEnd('\0');
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error parsing REG_BINARY as plain hex string: {ex.Message}");
                                return null;
                            }

                        case "REG_DWORD":
                            return "REG_DWORD";

                        default:
                            Console.WriteLine($"Unsupported parsed registry value type: {valueType}");
                            return null;
                    }
                }
            }

            return null;
        }
        
        private string ParseWineRegOutputEx(string output, string keyName)
        {
            var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string currentKey = null;
            string currentValue = null;

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Contains("="))
                {
                    var parts = line.Split(new[] { '=' }, 2);
                    currentKey = parts[0].Trim();
                    currentValue = parts[1].Trim();

                    string escapedString = @$"{currentValue}";
                    escapedString = escapedString.Replace("\\", "");
                    currentValue = escapedString;

                    if (currentKey.Contains(keyName))
                    {
                        if (currentValue.EndsWith(",\\"))
                        {
                            var multiLineValue = new StringBuilder(currentValue.TrimEnd('\\'));
                            while (currentValue.EndsWith(",\\"))
                            {
                                currentValue = lines[++i].Trim();
                                multiLineValue.Append(currentValue.TrimEnd('\\'));
                            }
                            currentValue = multiLineValue.ToString();
                        }

                        if (currentValue.StartsWith("dword:"))
                        {
                            return int.Parse(currentValue.Substring(6), System.Globalization.NumberStyles.HexNumber).ToString();
                        }
                        else if (currentValue.StartsWith("hex:"))
                        {
                            var hexValues = currentValue.Substring(4).Replace("\\", "").Split(',');
                            var bytes = hexValues.Select(hex => Convert.ToByte(hex, 16)).ToArray();
                            var decodedString = Encoding.UTF8.GetString(bytes);

                            if (decodedString.StartsWith("[") && decodedString.EndsWith("]"))
                            {
                                try
                                {
                                    var jsonObject = Newtonsoft.Json.JsonConvert.DeserializeObject(decodedString);
                                    return Newtonsoft.Json.JsonConvert.SerializeObject(jsonObject, Newtonsoft.Json.Formatting.Indented);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error parsing JSON: {ex.Message}");
                                    return decodedString;
                                }
                            }
                            else
                            {
                                return currentValue;
                            }
                        }
                        else
                        {
                            return currentValue;
                        }
                    }
                }
            }

            Console.WriteLine($"Key not found: {keyName}");
        
            return null;
        }

        public string GetVRChatWinePath()
        {
            string compatTool = GetSteamVdfCompatTool();
            if (compatTool == null)
            {
                Console.WriteLine("CompatTool not found");
                return null;
            }

            string steamPath = LogWatcher.GetSteamPath();
            string steamAppsCommonPath = Path.Combine(steamPath, "steamapps", "common");
            string compatabilityToolsPath = Path.Combine(steamPath, "compatibilitytools.d");
            string protonPath = Path.Combine(steamAppsCommonPath, compatTool);
            string compatToolPath = Path.Combine(compatabilityToolsPath, compatTool);
            string winePath = "";
            if (Directory.Exists(compatToolPath))
            {
                winePath = Path.Combine(compatToolPath, "files", "bin", "wine");
                if (!File.Exists(winePath))
                {
                    Console.WriteLine("Wine not found in CompatTool path");
                    return null;
                }
            }
            else if (Directory.Exists(protonPath))
            {
                winePath = Path.Combine(protonPath, "dist", "bin", "wine");
                if (!File.Exists(winePath))
                {
                    Console.WriteLine("Wine not found in Proton path");
                    return null;
                }
            }
            else
            {
                Console.WriteLine("CompatTool and Proton not found");
                return null;
            }

            return winePath;
        }

        private ProcessStartInfo GetWineProcessStartInfo(string winePath, string winePrefix, string wineCommand)
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{wineCommand.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            processStartInfo.Environment["WINEFSYNC"] = "1";
            processStartInfo.Environment["WINEPREFIX"] = winePrefix;
            //processStartInfo.Environment["WINEDEBUG"] = "-all";

            return processStartInfo;
        }

        private string GetWineRegCommand(string command)
        {
            string winePath = GetVRChatWinePath();
            string winePrefix = LogWatcher.GetVrcPrefixPath();
            string wineRegCommand = $"\"{winePath}\" reg {command}";
            ProcessStartInfo processStartInfo = GetWineProcessStartInfo(winePath, winePrefix, wineRegCommand);
            using (var process = Process.Start(processStartInfo))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(error) && 
                    !error.Contains("wineserver: using server-side synchronization.") && 
                    !error.Contains("fixme:wineusb:query_id"))
                {
                    Console.WriteLine($"Wine reg command error: {error}");
                    return null;
                }

                return output;
            }

            return null;
        }

        private string GetWineRegCommandEx(string regCommand)
        {
            string winePrefix = LogWatcher.GetVrcPrefixPath();
            string filePath = Path.Combine(winePrefix, "user.reg");
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Registry file not found at {filePath}");

            var match = Regex.Match(regCommand, @"^(add|query|delete)\s+""([^""]+)""(?:\s+/v\s+""([^""]+)"")?(?:\s+/t\s+(\w+))?(?:\s+/d\s+([^\s]+))?.*$");
            if (!match.Success)
                throw new ArgumentException("Invalid command format.");

            string action = match.Groups[1].Value.ToLower();
            string valueName = match.Groups[3].Success ? match.Groups[3].Value : null;
            string valueType = match.Groups[4].Success ? match.Groups[4].Value : null;
            string valueData = match.Groups[5].Success ? match.Groups[5].Value : null;

            var lines = File.ReadAllLines(filePath).ToList();
            var updatedLines = new List<string>();
            bool keyFound = false;
            bool valueFound = false;
            bool inVRChatSection = false;
            int headerEndIndex = -1;
            string keyHeader = "[Software\\\\VRChat\\\\VRChat]";

            if (action == "add")
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    string line = lines[i].Trim();

                    if (line.StartsWith(keyHeader))
                    {
                        inVRChatSection = true;
                        keyFound = true;
                        headerEndIndex = i;

                        // Add header and metadata lines
                        while (i < lines.Count && (lines[i].StartsWith("#") || lines[i].StartsWith("@") || lines[i].Trim().StartsWith(keyHeader)))
                        {
                            updatedLines.Add(lines[i]);
                            i++;
                        }
                        i--;
                        continue;
                    }
                    else if (inVRChatSection && line.StartsWith("["))
                    {
                        inVRChatSection = false;
                    }

                    if (inVRChatSection && valueName != null)
                    {
                        if (line.TrimStart().StartsWith($"\"{valueName}\"="))
                        {
                            valueFound = true;
                            updatedLines.Add($"\"{valueName}\"={GetRegistryValueFormat(valueType, valueData)}");
                            continue;
                        }
                    }

                    updatedLines.Add(lines[i]);
                }

                // Add new value if not found but section exists
                if (keyFound && !valueFound && valueName != null)
                {
                    var insertIndex = headerEndIndex + 2;
                    while (insertIndex < updatedLines.Count && 
                           (updatedLines[insertIndex].StartsWith("#") || updatedLines[insertIndex].StartsWith("@")))
                    {
                        insertIndex++;
                    }
                    updatedLines.Insert(insertIndex, $"\"{valueName}\"={GetRegistryValueFormat(valueType, valueData)}");
                }

                File.WriteAllLines(filePath, updatedLines);
                return $"Command '{regCommand}' executed successfully.";
            }
            else if (action == "query")
            {
                if (!valueName.Contains("_h"))
                {
                    valueName = AddHashToKeyName(valueName);
                }

                Console.WriteLine($"Querying registry values for {valueName}");
                foreach (var line in lines)
                {
                    if (line.Contains(valueName))
                    {
                        return line;
                    }
                }

                return $"Value \"{valueName}\" not found.";
            }

            Console.WriteLine($"Unsupported registry command: {regCommand}");

            return $"Command '{regCommand}' executed successfully.";
        }

        private static string GetRegistryValueFormat(string valueType, string valueData)
        {
            if (valueType?.ToUpper() == "REG_DWORD100")
            {
                double inputValue = double.Parse(valueData);
                Span<byte> dataBytes = stackalloc byte[sizeof(double)];
                BitConverter.TryWriteBytes(dataBytes, inputValue);
                var hexValues = dataBytes.ToArray().Select(b => b.ToString("X2")).ToArray();
                var byteString = string.Join(",", hexValues).ToLower();
                var result = $"hex(4):{byteString}";
                return result;
            }

            return valueType?.ToUpper() switch
            {
                "REG_DWORD" => $"dword:{int.Parse(valueData):X8}",
                _ => throw new ArgumentException($"Unsupported registry value type: {valueType}"),
            };
        }

        /// <summary>
        /// Retrieves the value of the specified key from the VRChat group in the windows registry.
        /// </summary>
        /// <param name="key">The name of the key to retrieve.</param>
        /// <returns>The value of the specified key, or null if the key does not exist.</returns>
        public string GetVRChatRegistryKey(string key)
        {
            try 
            {
                key = AddHashToKeyName(key);
                string regCommand = $"query \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\" /v \"{key}\"";
                var queryResult = GetWineRegCommand(regCommand);
                if (queryResult == null)
                    return null;

                var result = ParseWineRegOutput(queryResult, key);
                if (result == "REG_DWORD")
                {
                    queryResult = GetWineRegCommandEx(regCommand);
                    result = ParseWineRegOutputEx(queryResult, key);
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception in GetRegistryValueFromWine: {ex.Message}");
                return null;
            }
        }
#else
        /// <summary>
        /// Retrieves the value of the specified key from the VRChat group in the windows registry.
        /// </summary>
        /// <param name="key">The name of the key to retrieve.</param>
        /// <returns>The value of the specified key, or null if the key does not exist.</returns>
        public object GetVRChatRegistryKey(string key)
        {
            var keyName = AddHashToKeyName(key);
            using (var regKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\VRChat\VRChat"))
            {
                var data = regKey?.GetValue(keyName);
                if (data == null)
                    return null;

                var type = regKey.GetValueKind(keyName);
                switch (type)
                {
                    case RegistryValueKind.Binary:
                        return Encoding.ASCII.GetString((byte[])data);

                    case RegistryValueKind.DWord:
                        if (data.GetType() != typeof(long))
                            return data;

                        long.TryParse(data.ToString(), out var longValue);
                        var bytes = BitConverter.GetBytes(longValue);
                        var doubleValue = BitConverter.ToDouble(bytes, 0);
                        return doubleValue;
                }
            }

            return null;
        }
#endif
#if LINUX
        public async Task SetVRChatRegistryKeyAsync(string key, object value, int typeInt)
        {
            await Task.Run(() =>
            {
                SetVRChatRegistryKey(key, value, typeInt);
            });
        }
#endif  
        /// <summary>
        /// Sets the value of the specified key in the VRChat group in the windows registry.
        /// </summary>
        /// <param name="key">The name of the key to set.</param>
        /// <param name="value">The value to set for the specified key.</param>
        /// <param name="typeInt">The RegistryValueKind type.</param>
        /// <returns>True if the key was successfully set, false otherwise.</returns>
        public bool SetVRChatRegistryKey(string key, object value, int typeInt)
        {
            var type = (RegistryValueKind)typeInt;
            var keyName = AddHashToKeyName(key);
#if LINUX
            switch (type)
            {
                case RegistryValueKind.Binary:
                    if (value is JsonElement jsonElement)
                    {
                        
                        if (jsonElement.ValueKind == JsonValueKind.String)
                        {
                            byte[] byteArray = Encoding.UTF8.GetBytes(jsonElement.GetString());
                            var data = BitConverter.ToString(byteArray).Replace("-", "");
                            if (data.Length == 0)
                                data = "\"\"";
                            string regCommand = "add \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\" /v \"" + keyName + "\" /t REG_BINARY /d " + data + " /f";
                            var addResult = GetWineRegCommand(regCommand);
                            if (addResult == null)
                                return false;
                        }
                        else if (jsonElement.ValueKind == JsonValueKind.Array)
                        {
                            byte[] byteArray = jsonElement.EnumerateArray()
                                                           .Select(e => (byte)e.GetInt32()) // Convert each element to byte
                                                           .ToArray();
                            string regCommand = "add \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\" /v \"" + keyName + "\" /t REG_BINARY /d " + BitConverter.ToString(byteArray).Replace("-", "") + " /f";
                            var addResult = GetWineRegCommand(regCommand);
                            if (addResult == null)
                                return false;
                        }
                        else
                        {
                            Console.WriteLine($"Invalid value for REG_BINARY: {value}. It must be a JSON string or array.");
                            return false;
                        }
                    }
                    else if (value is string jsonArray)
                    {
                        byte[] byteArray = Encoding.UTF8.GetBytes(jsonArray);
                        string regCommand = "add \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\" /v \"" + keyName + "\" /t REG_BINARY /d " + BitConverter.ToString(byteArray).Replace("-", "") + " /f";
                        var addResult = GetWineRegCommand(regCommand);
                        if (addResult == null)
                            return false;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid value for REG_BINARY: {value}. It must be a JsonElement.");
                        return false;
                    }
                    break;
                
                case RegistryValueKind.DWord:
                    if (value is int intValue)
                    {
                        string regCommand = "add \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\" /v \"" + keyName + "\" /t REG_DWORD /d " + intValue + " /f";
                        var addResult = GetWineRegCommandEx(regCommand);
                        if (addResult == null)
                            return false;
                    }
                    else if (value is string stringValue && int.TryParse(stringValue, out int parsedIntValue))
                    {
                        string regCommand = "add \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\" /v \"" + keyName + "\" /t REG_DWORD /d " + parsedIntValue + " /f";
                        var addResult = GetWineRegCommandEx(regCommand);
                        if (addResult == null)
                            return false;
                    }
                    else if (value is JsonElement jsonElementValue && jsonElementValue.ValueKind == JsonValueKind.Number)
                    {
                        int parsedInt32Value = jsonElementValue.GetInt32();
                        string regCommand = "add \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\" /v \"" + keyName + "\" /t REG_DWORD /d " + parsedInt32Value + " /f";
                        var addResult = GetWineRegCommandEx(regCommand);
                        if (addResult == null)
                            return false;
                    }
                    else
                    {
                        Console.WriteLine($"Invalid value for REG_DWORD: {value}. It must be a valid integer.");
                        return false;
                    }
                    break;
                default:
                    Console.WriteLine($"Unsupported set registry value type: {typeInt}");
                    return false;
            }
#else          
            using (var regKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\VRChat\VRChat", true))
            {
                if (regKey == null)
                    return false;

                object setValue = null;
                switch (type)
                {
                    case RegistryValueKind.Binary:
                        setValue = Encoding.ASCII.GetBytes(value.ToString());
                        break;

                    case RegistryValueKind.DWord:
                        setValue = value;
                        break;
                }

                if (setValue == null)
                    return false;

                regKey.SetValue(keyName, setValue, type);
            }
#endif
            return true;
        }
  
        /// <summary>
        /// Sets the value of the specified key in the VRChat group in the windows registry.
        /// </summary>
        /// <param name="key">The name of the key to set.</param>
        /// <param name="value">The value to set for the specified key.</param>
        public void SetVRChatRegistryKey(string key, byte[] value)
        {
            var keyName = AddHashToKeyName(key);
#if LINUX
            var data = BitConverter.ToString(value).Replace("-", "");
            if (data.Length == 0)
                data = "\"\"";
            string regCommand = "add \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\" /v \"" + keyName + "\" /t REG_BINARY /d " + data + " /f";
            var addResult = GetWineRegCommand(regCommand);
            if (addResult == null)
                return;
#else
            var hKey = (UIntPtr)0x80000001; // HKEY_LOCAL_MACHINE
            const int keyWrite = 0x20006;
            const string keyFolder = @"SOFTWARE\VRChat\VRChat";
            var openKeyResult = RegOpenKeyEx(hKey, keyFolder, 0, keyWrite, out var folderPointer);
            if (openKeyResult != 0)
                throw new Exception("Error opening registry key. Error code: " + openKeyResult);

            var setKeyResult = RegSetValueEx(folderPointer, keyName, 0, RegistryValueKind.DWord, value, value.Length);
            if (setKeyResult != 0)
                throw new Exception("Error setting registry value. Error code: " + setKeyResult);

            RegCloseKey(hKey);
#endif
        }
#if LINUX
        public string GetVRChatRegistry()
#else
        public Dictionary<string, Dictionary<string, object>> GetVRChatRegistry()
#endif
        {
            var registry = new Dictionary<string, Dictionary<string, object>>();
#if LINUX
            string regCommand = "query \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\"";
            var queryResult = GetWineRegCommand(regCommand);
            if (queryResult == null)
                return null;

            var lines = queryResult.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(line => 
                    !string.IsNullOrWhiteSpace(line) && 
                    !line.Contains("fixme:") && 
                    !line.Contains("wine:"))
                .ToArray();

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(p => p.Trim())
                                .ToArray();
                if (parts.Length >= 3)
                {
                    var keyName = parts[0];
                    var index = keyName.LastIndexOf("_h", StringComparison.Ordinal);
                    if (index > 0)
                        keyName = keyName.Substring(0, index);
                    var valueType = parts[parts.Length - 2];
                    var value = parts[parts.Length - 1];
                    
                    switch (valueType)
                    {
                        case "REG_BINARY":
                            try 
                            {
                                // Treat the value as a plain hex string and decode it to ASCII
                                var hexValues = Enumerable.Range(0, value.Length / 2)
                                    .Select(i => value.Substring(i * 2, 2)) // Break string into chunks of 2
                                    .Select(hex => Convert.ToByte(hex, 16)) // Convert each chunk to a byte
                                    .ToArray();

                                var binDict = new Dictionary<string, object>
                                {
                                    { "data", Encoding.ASCII.GetString(hexValues).TrimEnd('\0') },
                                    { "type", 3 }
                                };
                                registry.Add(keyName, binDict);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error parsing REG_BINARY as plain hex string: {ex.Message}");
                            }
                            break;

                        case "REG_DWORD":
                            string regCommandExDword = $"query \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\" /v \"{keyName}\"";
                            var queryResultExDword = GetWineRegCommandEx(regCommandExDword);
                            if (queryResultExDword == null)
                                break;

                            var resultExDword = ParseWineRegOutputEx(queryResultExDword, keyName);
                            if (resultExDword == null)
                                break;

                            try
                            {
                                if (resultExDword.StartsWith("hex(4)"))
                                {
                                    string hexString = resultExDword;
                                    string[] hexValues = hexString.Split(':')[1].Split(',');
                                    byte[] byteValues = hexValues.Select(h => Convert.ToByte(h, 16)).ToArray();
                                    if (byteValues.Length != 8)
                                    {
                                        throw new ArgumentException("Input does not represent a valid 8-byte double-precision float.");
                                    }
                                    double parsedDouble = BitConverter.ToDouble(byteValues, 0);
                                    var doubleDict = new Dictionary<string, object>
                                    {
                                        { "data", parsedDouble },
                                        { "type", 100 } // it's special
                                    };
                                    registry.Add(keyName, doubleDict);
                                }
                                else
                                {
                                    // Convert dword value to integer
                                    int parsedInt = int.Parse(resultExDword);
                                    var dwordDict = new Dictionary<string, object>
                                    {
                                        { "data", parsedInt },
                                        { "type", 4 }
                                    };
                                    registry.Add(keyName, dwordDict);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error parsing REG_DWORD: {ex.Message}");
                            }
                            break;
                    }
                }
            }

            return Newtonsoft.Json.JsonConvert.SerializeObject(registry, Newtonsoft.Json.Formatting.Indented);
#else
            using (var regKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\VRChat\VRChat"))
            {
                if (regKey == null)
                    throw new Exception("Nothing to backup.");

                var keys = regKey.GetValueNames();

                Span<long> spanLong = stackalloc long[1];
                Span<double> doubleSpan = MemoryMarshal.Cast<long, double>(spanLong);

                foreach (var key in keys)
                {
                    var data = regKey.GetValue(key);
                    var index = key.LastIndexOf("_h", StringComparison.Ordinal);
                    if (index <= 0)
                        continue;

                    var keyName = key.Substring(0, index);
                    if (data == null)
                        continue;

                    var type = regKey.GetValueKind(key);
                    switch (type)
                    {
                        case RegistryValueKind.Binary:
                            var binDict = new Dictionary<string, object>
                            {
                                { "data", Encoding.ASCII.GetString((byte[])data) },
                                { "type", type }
                            };
                            registry.Add(keyName, binDict);
                            break;

                        case RegistryValueKind.DWord:
                            if (data.GetType() != typeof(long))
                            {
                                var dwordDict = new Dictionary<string, object>
                                {
                                    { "data", data },
                                    { "type", type }
                                };
                                registry.Add(keyName, dwordDict);
                                break;
                            }

                            spanLong[0] = (long)data;
                            var doubleValue = doubleSpan[0];
                            var floatDict = new Dictionary<string, object>
                            {
                                { "data", doubleValue },
                                { "type", 100 } // it's special
                            };
                            registry.Add(keyName, floatDict);
                            break;

                        default:
                            Debug.WriteLine($"Unknown registry value kind: {type}");
                            break;
                    }
                }
            }
            return registry;
#endif    
        }

        public void SetVRChatRegistry(string json)
        {
            CreateVRChatRegistryFolder();
#if LINUX
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json);
            foreach (var item in dict)
            {
                var data = (JsonElement)item.Value["data"];
                if (!int.TryParse(item.Value["type"].ToString(), out var type))
                    throw new Exception("Unknown type: " + item.Value["type"]);

                string keyName = AddHashToKeyName(item.Key);
                if (type == 4)
                {
                    int intValue = data.GetInt32();
                    string regCommand = "add \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\" /v \"" + keyName + "\" /t REG_DWORD /d " + intValue + " /f";
                    var addResult = GetWineRegCommandEx(regCommand);
                    if (addResult == null)
                        continue;
                }
                else if (type == 100)
                {
                    var valueLong = data.GetDouble();
                    string regCommand = "add \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\" /v \"" + keyName + "\" /t REG_DWORD100 /d " + valueLong + " /f";
                    var addResult = GetWineRegCommandEx(regCommand);
                    if (addResult == null)
                        continue;
                }
                else 
                {
                    // This slows down the recovery process but using async can be problematic
                    if (data.ValueKind == JsonValueKind.Number)
                    {
                        if (int.TryParse(data.ToString(), out var intValue))
                        {
                            SetVRChatRegistryKey(item.Key, intValue, type);
                            continue;
                        }
                        
                        throw new Exception("Unknown number type: " + item.Key);
                    }

                    SetVRChatRegistryKey(item.Key, data, type);
                }
            }
#else
            Span<double> spanDouble = stackalloc double[1];
            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, object>>>(json);
            foreach (var item in dict)
            {
                var data = (JsonElement)item.Value["data"];
                if (!int.TryParse(item.Value["type"].ToString(), out var type))
                    throw new Exception("Unknown type: " + item.Value["type"]);

                if (data.ValueKind == JsonValueKind.Number)
                {
                    if (type == 100)
                    {
                        // fun handling of double to long to byte array
                        spanDouble[0] = data.Deserialize<double>();
                        var valueLong = MemoryMarshal.Cast<double, long>(spanDouble)[0];
                        const int dataLength = sizeof(long);
                        var dataBytes = new byte[dataLength];
                        Buffer.BlockCopy(BitConverter.GetBytes(valueLong), 0, dataBytes, 0, dataLength);
                        SetVRChatRegistryKey(item.Key, dataBytes);
                        continue;
                    }

                    if (int.TryParse(data.ToString(), out var intValue))
                    {
                        SetVRChatRegistryKey(item.Key, intValue, type);
                        continue;
                    }

                    throw new Exception("Unknown number type: " + item.Key);
                }

                SetVRChatRegistryKey(item.Key, data, type);
            }
#endif
        }

        public bool HasVRChatRegistryFolder()
        {
#if LINUX
            string regCommand = "query \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\"";
            var queryResult = GetWineRegCommand(regCommand);
            if (queryResult == null)
                return false;

            return !string.IsNullOrEmpty(queryResult);
#endif
            using (var regKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\VRChat\VRChat"))
            {
                return regKey != null;
            }
        }

        public void CreateVRChatRegistryFolder()
        {
#if LINUX
            string regCommand = "add \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\" /f";
            var addResult = GetWineRegCommand(regCommand);
            if (addResult == null)
                return;
#endif
            if (HasVRChatRegistryFolder())
                return;

            using (var key = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\VRChat\VRChat"))
            {
                if (key == null)
                    throw new Exception("Error creating registry key.");
            }
        }

        public void DeleteVRChatRegistryFolder()
        {
#if LINUX
            string regCommand = "delete \"HKEY_CURRENT_USER\\SOFTWARE\\VRChat\\VRChat\" /f";
            var deleteResult = GetWineRegCommand(regCommand);
            if (deleteResult == null)
                return;
#else
            using (var regKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\VRChat\VRChat"))
            {
                if (regKey == null)
                    return;

                Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\VRChat\VRChat");
            }
#endif
        }

        /// <summary>
        /// Opens a file dialog to select a VRChat registry backup JSON file.
        /// </summary>
#if LINUX
#else
        public void OpenVrcRegJsonFileDialog()
        {
            if (dialogOpen) return;
            dialogOpen = true;

            var thread = new Thread(() =>
            {
                using (var openFileDialog = new System.Windows.Forms.OpenFileDialog())
                {
                    openFileDialog.DefaultExt = ".json";
                    openFileDialog.Filter = "JSON Files (*.json)|*.json";
                    openFileDialog.FilterIndex = 1;
                    openFileDialog.RestoreDirectory = true;

                    if (openFileDialog.ShowDialog() != DialogResult.OK)
                    {
                        dialogOpen = false;
                        return;
                    }

                    dialogOpen = false;

                    var path = openFileDialog.FileName;
                    if (string.IsNullOrEmpty(path))
                        return;

                    // return file contents
                    var json = File.ReadAllText(path);
                    ExecuteAppFunction("restoreVrcRegistryFromFile", json);
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }
#endif
    }
}