using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;
using FreeTaskBackend.Data;
using FreeTaskBackend.Models;

namespace FreeTaskBackend.Services;

public class FileService
{
    private readonly AppDbContext _context;
    private readonly ILogger<FileService> _logger;
    private readonly string _rootPath;

    public FileService(AppDbContext context, ILogger<FileService> logger)
    {
        _context = context;
        _logger = logger;
        _rootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
    }

    public async Task<string> UploadFileAsync(IFormFile file, string type, Guid userId)
    {
        try
        {
            _logger.LogInformation("Starting file upload for userId: {UserId}, type: {Type}, fileName: {FileName}",
                userId, type, file?.FileName);

            if (file == null || file.Length == 0)
            {
                _logger.LogWarning("Empty or null file received for userId: {UserId}", userId);
                throw new InvalidOperationException("Файл не предоставлен или пустой");
            }

            var allowedTypes = new[] { "jpg", "jpeg", "png", "pdf", "mp3", "mp4", "mov", "webm", "ogg" };
            var extension = Path.GetExtension(file.FileName)?.ToLower().TrimStart('.') ?? "webm";
            if (!allowedTypes.Contains(extension))
            {
                _logger.LogWarning("Invalid file extension: {Extension} for userId: {UserId}", extension, userId);
                throw new InvalidOperationException($"Недопустимый тип файла: {extension}");
            }

            if (file.Length > 10 * 1024 * 1024)
            {
                _logger.LogWarning("File size exceeds 10 MB limit: {FileSize} for userId: {UserId}", file.Length, userId);
                throw new InvalidOperationException("Файл превышает лимит в 10 МБ");
            }

            var fileId = Guid.NewGuid();
            var relativePath = $"/attachments/{type}/{userId}/{fileId}.{extension}";
            var filePath = Path.Combine(_rootPath, "attachments", type, userId.ToString(), $"{fileId}.{extension}");

            _logger.LogInformation("Preparing to save file to: {FilePath}", filePath);

            var directory = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.LogInformation("Created directory: {Directory}", directory);
            }

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
                _logger.LogInformation("File saved to: {FilePath}, Size: {Size}", filePath, file.Length);
            }

            if (!File.Exists(filePath))
            {
                _logger.LogError("File not found after saving: {FilePath}", filePath);
                throw new InvalidOperationException("Файл не был сохранён");
            }

            var fileAttachment = new FileAttachment
            {
                Id = fileId,
                ChatId = null,
                MessageId = null,
                OwnerId = userId,
                FilePath = relativePath,
                FileType = extension,
                Size = file.Length,
                CreatedAt = DateTime.UtcNow
            };

            _context.FileAttachments.Add(fileAttachment);
            await _context.SaveChangesAsync();
            _logger.LogInformation("File attachment saved to DB: {FileId}, Path: {RelativePath}", fileId, relativePath);

            return relativePath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload file for userId: {UserId}, type: {Type}, fileName: {FileName}",
                userId, type, file?.FileName);
            throw new InvalidOperationException($"Ошибка при загрузке файла: {ex.Message}", ex);
        }
    }

    public string GetFilePath(Guid fileId)
    {
        var file = _context.FileAttachments.FirstOrDefault(f => f.Id == fileId)
            ?? throw new KeyNotFoundException("Файл не найден");

        // Преобразуем относительный путь в абсолютный
        var absolutePath = Path.Combine(_rootPath, file.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        return absolutePath;
    }

    public string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        return extension switch
        {
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            ".mp3" => "audio/mpeg",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            _ => "application/octet-stream"
        };
    }

    public async Task DeleteFileAsync(Guid fileId)
    {
        var file = _context.FileAttachments.FirstOrDefault(f => f.Id == fileId)
            ?? throw new KeyNotFoundException("Файл не найден");

        // Преобразуем относительный путь в абсолютный для удаления
        var absolutePath = Path.Combine(_rootPath, file.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
            _logger.LogInformation("File deleted: {FilePath}", absolutePath);
        }

        _context.FileAttachments.Remove(file);
        await _context.SaveChangesAsync();
        _logger.LogInformation("File attachment removed from DB: {FileId}", fileId);
    }

    public async Task<FileAttachment> GetFileAsync(Guid fileId)
    {
        var file = await _context.FileAttachments.FirstOrDefaultAsync(f => f.Id == fileId);
        if (file == null)
        {
            _logger.LogWarning("File not found in DB: {FileId}", fileId);
            throw new KeyNotFoundException("Файл не найден");
        }
        return file;
    }
}