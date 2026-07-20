using Attendance.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Attendance.Infrastructure.Helpers;

/// <summary>
/// Persists staff profile photos to the local file system under
/// <c>wwwroot/staff-photos/{staffCode}/</c> and returns the relative path
/// suitable for storage in <c>StaffProfile.PhotoPath</c>.
/// </summary>
public sealed class LocalFileStorageHelper : IFileStorageHelper
{
    private readonly string _webRootPath;

    /// <summary>
    /// Initialises the helper using the web host environment to resolve
    /// the physical path of <c>wwwroot</c>.
    /// </summary>
    public LocalFileStorageHelper(IWebHostEnvironment env)
    {
        _webRootPath = env.WebRootPath
            ?? throw new ArgumentNullException(nameof(env), "WebRootPath is not configured.");
    }

    /// <inheritdoc />
    /// <remarks>
    /// The file is saved to <c>{wwwroot}/staff-photos/{staffCode}/{staffCode}.png</c>.
    /// Any existing file at that path is overwritten so that the profile always
    /// reflects the most recent upload.
    /// The returned relative path uses forward slashes and is safe to embed in
    /// an <c>&lt;img&gt;</c> src attribute:
    /// <c>staff-photos/{staffCode}/{staffCode}.png</c>.
    /// </remarks>
    public async Task<string> SavePhotoAsync(IFormFile file, string staffCode)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(staffCode);

        // Build the physical directory path
        var directory = Path.Combine(_webRootPath, "staff-photos", staffCode);
        Directory.CreateDirectory(directory);

        // Use a deterministic file name derived from the staff code so that
        // re-uploads always replace the previous photo without leaving orphans.
        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".png";

        var fileName = $"{staffCode}{extension}";
        var physicalPath = Path.Combine(directory, fileName);

        await using var stream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write);
        await file.CopyToAsync(stream);

        // Return a relative URL path (forward-slash separated) for storage in the DB.
        return $"staff-photos/{staffCode}/{fileName}";
    }

    /// <inheritdoc />
    /// <remarks>
    /// <paramref name="filePath"/> may be either the relative path returned by
    /// <see cref="SavePhotoAsync"/> (e.g. <c>staff-photos/EMP-0001/EMP-0001.png</c>)
    /// or an absolute physical path.  If the file does not exist the call is a
    /// no-op — no exception is thrown.
    /// </remarks>
    public void DeletePhoto(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        // Resolve to a physical path when a relative path is supplied.
        var physicalPath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(_webRootPath, filePath.Replace('/', Path.DirectorySeparatorChar));

        if (File.Exists(physicalPath))
            File.Delete(physicalPath);
    }
}
