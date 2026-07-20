using Microsoft.AspNetCore.Http;

namespace Attendance.Application.Interfaces;

/// <summary>
/// Abstracts the file system (or cloud storage) operations needed to persist
/// and remove staff profile photos.  Implementations live in the Infrastructure
/// layer, keeping the Application layer free of I/O concerns.
/// </summary>
public interface IFileStorageHelper
{
    /// <summary>
    /// Saves an uploaded photo file to durable storage and returns the
    /// relative or absolute path under which it was persisted.
    /// </summary>
    /// <param name="file">
    /// The uploaded file from a multipart/form-data request.
    /// The file must already have passed PNG validation before this call.
    /// </param>
    /// <param name="staffCode">
    /// The staff's <c>UniqueCode</c> (e.g., <c>EMP-0001</c>), used to
    /// derive a deterministic, collision-free file name.
    /// </param>
    /// <returns>
    /// The storage path (or URL) of the saved file, stored in
    /// <c>StaffProfile.PhotoPath</c>.
    /// </returns>
    Task<string> SavePhotoAsync(IFormFile file, string staffCode);

    /// <summary>
    /// Removes a previously saved photo file from storage.
    /// If the file does not exist the call is a no-op (no exception thrown).
    /// </summary>
    /// <param name="filePath">
    /// The path returned by a prior call to <see cref="SavePhotoAsync"/>.
    /// </param>
    void DeletePhoto(string filePath);
}
