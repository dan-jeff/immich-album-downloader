using ImmichDownloader.Web.Validation;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Secure file service implementation that provides safe file operations with path validation.
/// Prevents directory traversal attacks and unauthorized file access outside allowed directories.
/// </summary>
public class SecureFileService : ISecureFileService
{
    private readonly ILogger<SecureFileService> _logger;
    private readonly IConfiguration _configuration;
    private readonly string _downloadDirectory;
    private readonly string _tempDirectory;

    public SecureFileService(ILogger<SecureFileService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
        
        // Initialize secure directories
        var dataPath = configuration.GetValue<string>("DataPath") ?? "data";
        // Ensure absolute path for Docker container
        if (!Path.IsPathRooted(dataPath))
        {
            dataPath = Path.Combine("/app", dataPath);
        }
        _downloadDirectory = InitializeSecureDirectory("Downloads", Path.Combine(dataPath, "downloads"));
        _tempDirectory = InitializeSecureDirectory("Temp", Path.Combine(dataPath, "temp"));
    }

    /// <summary>
    /// Validates that a file path is within the allowed base directory and is safe to access.
    /// </summary>
    public ValidationResult ValidateFilePath(string filePath, string allowedBasePath)
    {
        var result = new ValidationResult();
        
        if (string.IsNullOrWhiteSpace(filePath))
        {
            result.Errors.Add("File path cannot be empty");
            return result;
        }

        if (string.IsNullOrWhiteSpace(allowedBasePath))
        {
            result.Errors.Add("Allowed base path cannot be empty");
            return result;
        }

        try
        {
            // Check for null bytes and control characters BEFORE path normalization
            if (ContainsNullBytes(filePath))
            {
                result.Errors.Add("File path contains null bytes or control characters");
                _logger.LogWarning("Null byte injection attempt detected: {FilePath}", filePath);
                return result;
            }
            
            // Check for URL encoding attempts BEFORE path normalization
            if (ContainsUrlEncoding(filePath))
            {
                result.Errors.Add("File path contains URL encoding");
                _logger.LogWarning("URL encoding detected in file path: {FilePath}", filePath);
                return result;
            }
            
            // Check for Windows-style directory traversal BEFORE path normalization
            if (Environment.OSVersion.Platform == PlatformID.Unix && ContainsWindowsPathSeparators(filePath))
            {
                result.Errors.Add("File path contains Windows-style path separators");
                _logger.LogWarning("Windows path separators detected on Unix system: {FilePath}", filePath);
                return result;
            }
            
            // Resolve and normalize paths first
            // Combine the file path with the base path to get the full path
            var combinedPath = Path.Combine(allowedBasePath, filePath);
            var fullFilePath = Path.GetFullPath(combinedPath);
            var fullBasePath = Path.GetFullPath(allowedBasePath);
            
            // Ensure the file path is within the allowed base path (this is the real security check)
            if (!fullFilePath.StartsWith(fullBasePath, StringComparison.OrdinalIgnoreCase))
            {
                result.Errors.Add("File path is outside allowed directory");
                _logger.LogWarning("Directory traversal attempt detected: {FilePath} outside {BasePath}", 
                    filePath, allowedBasePath);
                return result;
            }
            
            // Additional check for suspicious patterns that might have bypassed normalization
            if (ContainsObviousTraversalPatterns(filePath))
            {
                result.Errors.Add("File path contains suspicious traversal patterns");
                _logger.LogWarning("Suspicious traversal patterns detected: {FilePath}", filePath);
                return result;
            }
            
            // Check for symbolic links (security risk)
            if (IsSymbolicLink(fullFilePath))
            {
                result.Errors.Add("Symbolic links are not allowed");
                _logger.LogWarning("Symbolic link detected: {FilePath}", filePath);
                return result;
            }

            // Additional validation using InputValidator
            if (!InputValidator.IsValidFilePath(filePath, allowedBasePath))
            {
                result.Errors.Add("File path contains invalid characters or patterns");
                return result;
            }

            // Check for suspicious file names
            var fileName = Path.GetFileName(filePath);
            if (ContainsSuspiciousPatterns(fileName))
            {
                result.Errors.Add("File name contains suspicious patterns");
                _logger.LogWarning("Suspicious file name detected: {FileName}", fileName);
                return result;
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Path validation failed: {ex.Message}");
            _logger.LogError(ex, "Error validating file path {FilePath}", filePath);
        }

        return result;
    }

    /// <summary>
    /// Safely reads a file ensuring it's within the allowed directory structure.
    /// </summary>
    public async Task<byte[]> ReadFileAsync(string filePath, string allowedBasePath)
    {
        var validation = ValidateFilePath(filePath, allowedBasePath);
        if (!validation.IsValid)
        {
            throw new UnauthorizedAccessException($"File access denied: {string.Join(", ", validation.Errors)}");
        }

        var fullPath = Path.GetFullPath(Path.Combine(allowedBasePath, filePath));
        
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        try
        {
            _logger.LogInformation("Reading file: {FilePath}", filePath);
            return await File.ReadAllBytesAsync(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading file {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Safely opens a file stream ensuring it's within the allowed directory structure.
    /// </summary>
    public FileStream OpenFileStream(string filePath, string allowedBasePath, FileMode mode = FileMode.Open, 
        FileAccess access = FileAccess.Read, FileShare share = FileShare.Read)
    {
        var validation = ValidateFilePath(filePath, allowedBasePath);
        if (!validation.IsValid)
        {
            throw new UnauthorizedAccessException($"File access denied: {string.Join(", ", validation.Errors)}");
        }

        var fullPath = Path.GetFullPath(Path.Combine(allowedBasePath, filePath));
        
        // Ensure directory exists for write operations
        if (mode == FileMode.Create || mode == FileMode.CreateNew || mode == FileMode.OpenOrCreate)
        {
            var directory = Path.GetDirectoryName(fullPath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        try
        {
            _logger.LogInformation("Opening file stream: {FilePath} (Mode: {Mode}, Access: {Access})", 
                filePath, mode, access);
            return new FileStream(fullPath, mode, access, share);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error opening file stream {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Safely writes data to a file ensuring it's within the allowed directory structure.
    /// </summary>
    public async Task WriteFileAsync(string filePath, string allowedBasePath, byte[] data)
    {
        var validation = ValidateFilePath(filePath, allowedBasePath);
        if (!validation.IsValid)
        {
            throw new UnauthorizedAccessException($"File access denied: {string.Join(", ", validation.Errors)}");
        }

        var fullPath = Path.GetFullPath(Path.Combine(allowedBasePath, filePath));
        
        // Ensure directory exists
        var directory = Path.GetDirectoryName(fullPath);
        if (directory != null && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        try
        {
            _logger.LogInformation("Writing file: {FilePath} ({Size} bytes)", filePath, data.Length);
            await File.WriteAllBytesAsync(fullPath, data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing file {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Safely deletes a file ensuring it's within the allowed directory structure.
    /// </summary>
    public bool DeleteFile(string filePath, string allowedBasePath)
    {
        var validation = ValidateFilePath(filePath, allowedBasePath);
        if (!validation.IsValid)
        {
            throw new UnauthorizedAccessException($"File access denied: {string.Join(", ", validation.Errors)}");
        }

        var fullPath = Path.GetFullPath(Path.Combine(allowedBasePath, filePath));
        
        if (!File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            _logger.LogInformation("Deleting file: {FilePath}", filePath);
            File.Delete(fullPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file {FilePath}", filePath);
            throw;
        }
    }

    /// <summary>
    /// Checks if a file exists within the allowed directory structure.
    /// </summary>
    public bool FileExists(string filePath, string allowedBasePath)
    {
        var validation = ValidateFilePath(filePath, allowedBasePath);
        if (!validation.IsValid)
        {
            _logger.LogWarning("File path validation failed for '{FilePath}' in '{AllowedBasePath}': {Errors}",
                filePath, allowedBasePath, string.Join(", ", validation.Errors));
            return false; // Don't throw exception, just return false for security
        }

        try
        {
            var fullPath = Path.GetFullPath(Path.Combine(allowedBasePath, filePath));
            var exists = File.Exists(fullPath);
            _logger.LogInformation("File existence check: '{FullPath}' exists: {Exists}", fullPath, exists);
            return exists;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking file existence for '{FilePath}' in '{AllowedBasePath}'", filePath, allowedBasePath);
            return false; // Don't leak path information through exceptions
        }
    }

    /// <summary>
    /// Creates a secure directory path within the allowed base directory.
    /// </summary>
    public string CreateSecureDirectory(string directoryPath, string allowedBasePath)
    {
        var validation = ValidateFilePath(directoryPath, allowedBasePath);
        if (!validation.IsValid)
        {
            throw new UnauthorizedAccessException($"Directory access denied: {string.Join(", ", validation.Errors)}");
        }

        var fullPath = Path.GetFullPath(directoryPath);

        try
        {
            if (!Directory.Exists(fullPath))
            {
                _logger.LogInformation("Creating directory: {DirectoryPath}", directoryPath);
                Directory.CreateDirectory(fullPath);
            }
            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating directory {DirectoryPath}", directoryPath);
            throw;
        }
    }

    /// <summary>
    /// Gets the allowed download directory path for the application.
    /// </summary>
    public string GetDownloadDirectory()
    {
        return _downloadDirectory;
    }

    /// <summary>
    /// Gets the allowed temporary directory path for the application.
    /// </summary>
    public string GetTemporaryDirectory()
    {
        return _tempDirectory;
    }

    /// <summary>
    /// Initializes a secure directory and returns its full path.
    /// </summary>
    private string InitializeSecureDirectory(string configKey, string defaultPath)
    {
        var configuredPath = _configuration[$"SecureDirectories:{configKey}"];
        var directoryPath = !string.IsNullOrEmpty(configuredPath) ? configuredPath : defaultPath;
        
        try
        {
            var fullPath = Path.GetFullPath(directoryPath);
            
            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                _logger.LogInformation("Created secure directory: {DirectoryPath}", fullPath);
            }
            
            // Set proper permissions (Linux/Docker)
            if (Environment.OSVersion.Platform == PlatformID.Unix)
            {
                try
                {
                    // Set directory permissions to 755 (owner: rwx, group/others: rx)
                    var chmod = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "chmod",
                            Arguments = $"755 \"{fullPath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }
                    };
                    chmod.Start();
                    chmod.WaitForExit();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not set directory permissions for {DirectoryPath}", fullPath);
                }
            }
            
            return fullPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize secure directory {ConfigKey} at {Path}", configKey, directoryPath);
            throw;
        }
    }

    /// <summary>
    /// Checks if a file name contains suspicious patterns that might indicate malicious intent.
    /// </summary>
    private static bool ContainsSuspiciousPatterns(string fileName)
    {
        var suspiciousPatterns = new[]
        {
            "..", "~", "$", "|", "&", ";", "`", 
            "<?", "?>", "<script", "</script>",
            ".exe", ".bat", ".cmd", ".ps1", ".sh",
            "web.config", ".htaccess", ".env"
        };

        return suspiciousPatterns.Any(pattern => 
            fileName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Checks if a path contains null bytes or control characters that could be used for attacks.
    /// </summary>
    private static bool ContainsNullBytes(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
            
        // Check for null bytes and control characters
        return path.Any(c => c == '\0' || c == '\n' || c == '\r' || c == '\t' || char.IsControl(c));
    }
    
    /// <summary>
    /// Checks if a path contains URL encoding that could be used to bypass security checks.
    /// </summary>
    private static bool ContainsUrlEncoding(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
            
        // Check for common URL encoding patterns
        var urlEncodingPatterns = new[]
        {
            "%2e", "%2f", "%5c", "%00", "%0a", "%0d", "%09",
            "%2E", "%2F", "%5C", "%20", "%22", "%3C", "%3E"
        };
        
        return urlEncodingPatterns.Any(pattern => 
            path.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Checks if a path contains Windows-style path separators that could be used for traversal attacks on Unix systems.
    /// </summary>
    private static bool ContainsWindowsPathSeparators(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
            
        // Check for backslash characters which are valid on Windows but suspicious on Unix
        return path.Contains('\\');
    }
    
    /// <summary>
    /// Checks if a path contains directory traversal patterns before normalization.
    /// </summary>
    private static bool ContainsDirectoryTraversalPatterns(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
            
        var traversalPatterns = new[]
        {
            "..", "../", "..\\"  // Basic traversal patterns
        };
        
        return traversalPatterns.Any(pattern => path.Contains(pattern));
    }
    
    /// <summary>
    /// Checks for obvious traversal patterns that should be rejected even if they normalize safely.
    /// This catches sophisticated attacks that might bypass path normalization.
    /// </summary>
    private static bool ContainsObviousTraversalPatterns(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;
            
        // Check for multiple consecutive .. patterns or very deep traversal attempts
        var suspiciousPatterns = new[]
        {
            "../../../",   // Deep traversal attempt
            "..\\..\\..\\", // Deep traversal attempt (Windows)
            "....//",      // Double-dot bypass attempt
            "..%2f",       // URL encoded slash with ..
            "..%5c"        // URL encoded backslash with ..
        };
        
        return suspiciousPatterns.Any(pattern => path.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Checks if a file path points to a symbolic link.
    /// </summary>
    private static bool IsSymbolicLink(string path)
    {
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
                return false;
                
            var fileInfo = new FileInfo(path);
            return fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint);
        }
        catch
        {
            // If we can't determine, assume it's safe
            return false;
        }
    }
}