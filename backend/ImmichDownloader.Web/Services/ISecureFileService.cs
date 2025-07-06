using ImmichDownloader.Web.Validation;

namespace ImmichDownloader.Web.Services;

/// <summary>
/// Interface for secure file operations with path validation and access controls.
/// Provides methods for safely accessing files while preventing directory traversal and unauthorized access.
/// </summary>
public interface ISecureFileService
{
    /// <summary>
    /// Validates that a file path is within the allowed base directory and is safe to access.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <param name="allowedBasePath">The base directory that files must be within.</param>
    /// <returns>A validation result indicating whether the path is safe.</returns>
    ValidationResult ValidateFilePath(string filePath, string allowedBasePath);

    /// <summary>
    /// Safely reads a file ensuring it's within the allowed directory structure.
    /// </summary>
    /// <param name="filePath">The path to the file to read.</param>
    /// <param name="allowedBasePath">The base directory that files must be within.</param>
    /// <returns>The file contents as a byte array.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when file access is not allowed.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist.</exception>
    Task<byte[]> ReadFileAsync(string filePath, string allowedBasePath);

    /// <summary>
    /// Safely opens a file stream ensuring it's within the allowed directory structure.
    /// </summary>
    /// <param name="filePath">The path to the file to open.</param>
    /// <param name="allowedBasePath">The base directory that files must be within.</param>
    /// <param name="mode">The file mode for opening the file.</param>
    /// <param name="access">The file access mode.</param>
    /// <param name="share">The file sharing mode.</param>
    /// <returns>A FileStream for the requested file.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when file access is not allowed.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist.</exception>
    FileStream OpenFileStream(string filePath, string allowedBasePath, FileMode mode = FileMode.Open, 
        FileAccess access = FileAccess.Read, FileShare share = FileShare.Read);

    /// <summary>
    /// Safely writes data to a file ensuring it's within the allowed directory structure.
    /// </summary>
    /// <param name="filePath">The path to the file to write.</param>
    /// <param name="allowedBasePath">The base directory that files must be within.</param>
    /// <param name="data">The data to write to the file.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when file access is not allowed.</exception>
    Task WriteFileAsync(string filePath, string allowedBasePath, byte[] data);

    /// <summary>
    /// Safely deletes a file ensuring it's within the allowed directory structure.
    /// </summary>
    /// <param name="filePath">The path to the file to delete.</param>
    /// <param name="allowedBasePath">The base directory that files must be within.</param>
    /// <returns>True if the file was deleted, false if it didn't exist.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when file access is not allowed.</exception>
    bool DeleteFile(string filePath, string allowedBasePath);

    /// <summary>
    /// Checks if a file exists within the allowed directory structure.
    /// </summary>
    /// <param name="filePath">The path to check.</param>
    /// <param name="allowedBasePath">The base directory that files must be within.</param>
    /// <returns>True if the file exists and is accessible, false otherwise.</returns>
    bool FileExists(string filePath, string allowedBasePath);

    /// <summary>
    /// Creates a secure directory path within the allowed base directory.
    /// </summary>
    /// <param name="directoryPath">The directory path to create.</param>
    /// <param name="allowedBasePath">The base directory that directories must be within.</param>
    /// <returns>The full path of the created directory.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when directory creation is not allowed.</exception>
    string CreateSecureDirectory(string directoryPath, string allowedBasePath);

    /// <summary>
    /// Gets the allowed download directory path for the application.
    /// </summary>
    /// <returns>The secure download directory path.</returns>
    string GetDownloadDirectory();

    /// <summary>
    /// Gets the allowed temporary directory path for the application.
    /// </summary>
    /// <returns>The secure temporary directory path.</returns>
    string GetTemporaryDirectory();
}