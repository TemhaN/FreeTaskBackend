using System.ComponentModel.DataAnnotations;

namespace FreeTaskBackend.Attributes;

public class FileSizeAttribute : ValidationAttribute
{
    private readonly long _maxSize;

    public FileSizeAttribute(long maxSize)
    {
        _maxSize = maxSize;
    }

    protected override ValidationResult IsValid(object value, ValidationContext validationContext)
    {
        if (value is IFormFile file && file.Length > _maxSize)
            return new ValidationResult($"File size exceeds {_maxSize / 1_000_000} MB.");
        return ValidationResult.Success;
    }
}