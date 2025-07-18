using FreeTaskBackend.Data;
using FreeTaskBackend.Models;
using FreeTaskBackend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;

namespace FreeTaskBackend.Controllers;

[ApiController]
[Route("api/v1/files")]
public class FilesController : ControllerBase
{
    private readonly FileService _fileService;
    private readonly ILogger<FilesController> _logger;

    public FilesController(FileService fileService, ILogger<FilesController> logger)
    {
        _fileService = fileService;
        _logger = logger;
    }

    [Authorize]
    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile([FromForm] UploadFileDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new { message = "Неверный токен" });

        try
        {
            var fileUrl = await _fileService.UploadFileAsync(dto.File, dto.Type, userId);
            return Ok(new { FileUrl = fileUrl });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file for user: {UserId}", userId);
            return BadRequest(new { message = "Ошибка при загрузке файла" });
        }
    }

    [HttpGet("{fileId}")]
    public async Task<IActionResult> GetFile(Guid fileId)
    {
        try
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
                return Unauthorized(new { message = "Неверный токен" });

            var file = await _fileService.GetFileAsync(fileId);
            if (file == null || (file.OwnerId != userId && !User.IsInRole("Admin")))
                return NotFound("File not found or access denied.");

            return PhysicalFile(file.FilePath, file.ContentType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving file: {FileId}", fileId);
            return BadRequest(new { message = "Ошибка при получении файла" });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{fileId}")]
    public async Task<IActionResult> DeleteFile(Guid fileId)
    {
        try
        {
            await _fileService.DeleteFileAsync(fileId);
            return Ok(new { message = "Файл удален" });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file: {FileId}", fileId);
            return BadRequest(new { message = "Ошибка при удалении файла" });
        }
    }
    [HttpGet("check/{fileId}")]
    public async Task<IActionResult> CheckFile(Guid fileId)
    {
        try
        {
            var file = await _fileService.GetFileAsync(fileId);
            var absolutePath = _fileService.GetFilePath(fileId);
            return Ok(new
            {
                file.FilePath,
                AbsolutePath = absolutePath,
                Exists = System.IO.File.Exists(absolutePath)
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound("Файл с таким ID не найден");
        }
    }

}